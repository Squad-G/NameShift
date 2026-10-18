using System;
using System.IO;

namespace NameShift.Ui;

/// <summary>The outcome of reading a typed path.</summary>
public enum PathInputResult
{
    Ok,
    Empty,
    Invalid,
}

/// <summary>Reads and normalises a folder path typed in the path bar. Results are identical on .NET Framework and .NET.</summary>
public static class PathInput
{
    private const string BadChars = "<>\"|?*";

    /// <summary>Normalises <paramref name="text"/>; <paramref name="path"/> is empty unless the result is Ok.</summary>
    public static PathInputResult TryNormalize(string? text, out string path)
    {
        path = "";
        string value = (text ?? "").Trim();
        if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
        {
            value = value.Substring(1, value.Length - 2).Trim();
        }

        if (value.Length == 0)
        {
            return PathInputResult.Empty;
        }

        value = Environment.ExpandEnvironmentVariables(value);
        if (HasForbiddenCharacter(value)
            || value.StartsWith(@"\\?\", StringComparison.Ordinal)
            || value.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            return PathInputResult.Invalid;
        }

        value = value.Replace('/', '\\');
        if (!IsFullyQualified(value))
        {
            return PathInputResult.Invalid;
        }

        if (value.Length == 2)
        {
            value += "\\";
        }

        string full;
        try
        {
            full = Path.GetFullPath(value);
        }
        catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is PathTooLongException)
        {
            return PathInputResult.Invalid;
        }

        path = TrimTrailingSeparators(full);
        return PathInputResult.Ok;
    }

    /// <summary>Same folder: ordinal, case-insensitive, ignoring a trailing backslash (drive roots keep theirs).</summary>
    public static bool SameFolder(string a, string b) =>
        string.Equals(TrimTrailingSeparators(a), TrimTrailingSeparators(b), StringComparison.OrdinalIgnoreCase);

    private static bool HasForbiddenCharacter(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c < ' ' || BadChars.IndexOf(c) >= 0 || (c == ':' && i != 1))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFullyQualified(string value)
    {
        if (value.Length >= 2 && value[1] == ':')
        {
            char letter = value[0];
            bool isLetter = (letter >= 'A' && letter <= 'Z') || (letter >= 'a' && letter <= 'z');
            return isLetter && (value.Length == 2 || value[2] == '\\');
        }

        if (value.StartsWith(@"\\", StringComparison.Ordinal))
        {
            string[] parts = value.Substring(2).Split('\\');
            return parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0;
        }

        return false;
    }

    private static string TrimTrailingSeparators(string path)
    {
        string trimmed = path.TrimEnd('\\');
        if (trimmed.Length == 2 && trimmed[1] == ':')
        {
            return trimmed + "\\";
        }

        return trimmed.Length == 0 ? path : trimmed;
    }
}
