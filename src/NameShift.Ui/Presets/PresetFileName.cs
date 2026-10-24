using System;
using System.Globalization;
using System.Text;

namespace NameShift.Ui.Presets;

/// <summary>The safe file name of a preset: a name Windows accepts, at most 100 characters, ending ".json", with no
/// separator, so <c>Path.Combine(folder, fileName)</c> stays in the folder. The name shown to the user is the one
/// inside the file; this is only where it is stored.</summary>
internal static class PresetFileName
{
    /// <summary>The longest base, in UTF-16 units, before the reserved-name "_", the copy suffix and ".json": 88 + 1 +
    /// " (999)" + ".json" is 100.</summary>
    internal const int MaxBaseLength = 88;

    private const string Fallback = "preset";

    // An explicit list: Path.GetInvalidFileNameChars() differs between runtimes. Characters below U+0020 are refused too.
    private const string Refused = "<>:\"/\\|?*";

    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",

        // Windows also counts the superscript digits 1, 2 and 3 as COM and LPT digits.
        "COM\u00B9", "COM\u00B2", "COM\u00B3", "LPT\u00B9", "LPT\u00B2", "LPT\u00B3",
    };

    /// <summary>The file name for a preset name; <paramref name="copy"/> 1 has no suffix, 2 and up add " (n)".</summary>
    /// <exception cref="ArgumentNullException">The name is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The copy is below 1.</exception>
    public static string For(string name, int copy)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (copy < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(copy), copy, "The copy starts at 1.");
        }

        string safe = Tidy(ReplaceRefused(name));
        if (safe.Length > MaxBaseLength)
        {
            int cut = MaxBaseLength;
            if (char.IsLowSurrogate(safe[cut]) && char.IsHighSurrogate(safe[cut - 1]))
            {
                // Never between the two halves of a pair.
                cut--;
            }

            safe = Tidy(safe.Substring(0, cut));
        }

        // Spaces before the first dot do not count ("con .txt" is the device on Windows 10).
        string stem = safe.Substring(0, StemLength(safe)).TrimEnd(' ');
        if (IsReserved(stem))
        {
            // After the stem, so the part before the first dot is no longer a device name: "con.txt" is "con_.txt".
            safe = safe.Insert(stem.Length, "_");
        }

        string suffix = copy > 1 ? " (" + copy.ToString(CultureInfo.InvariantCulture) + ")" : "";
        return safe + suffix + ".json";
    }

    private static string ReplaceRefused(string name)
    {
        var builder = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]))
            {
                // A valid pair (an emoji) is kept whole.
                builder.Append(c).Append(name[i + 1]);
                i++;
            }
            else if (c < ' ' || Refused.IndexOf(c) >= 0 || char.IsSurrogate(c))
            {
                builder.Append('_');
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Trims white space at both ends and trailing dots and spaces; "preset" when nothing is left.</summary>
    private static string Tidy(string name)
    {
        string trimmed = name.Trim().TrimEnd('.', ' ').Trim();
        return trimmed.Length == 0 ? Fallback : trimmed;
    }

    /// <summary>The length of the part before the first dot.</summary>
    private static int StemLength(string name)
    {
        int dot = name.IndexOf('.');
        return dot < 0 ? name.Length : dot;
    }

    /// <summary>True when the stem is a reserved device name, ignoring case.</summary>
    private static bool IsReserved(string stem)
    {
        foreach (string reserved in ReservedNames)
        {
            if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
