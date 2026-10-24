using System;
using System.Globalization;

namespace NameShift.Core;

/// <summary>Adds the item's modified or created date (local time, as the app read it; never converted) before or
/// after the base name. The date text is a .NET custom format in the invariant culture; time-zone letters are refused
/// so names never depend on the PC. The extension is never touched.</summary>
public sealed class AutoDateRule : IRenameRule
{
    private static readonly DateTime Sample = new DateTime(2024, 3, 9, 14, 5, 7);

    /// <summary>Creates the rule.</summary>
    /// <exception cref="ArgumentNullException">format or separator is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">source or placement is not a defined value.</exception>
    /// <exception cref="ArgumentException">Validate refuses the format.</exception>
    public AutoDateRule(DateSource source = DateSource.Modified, string format = "yyyy-MM-dd",
        DatePlacement placement = DatePlacement.Prefix, string separator = "_")
    {
        if (!Enum.IsDefined(typeof(DateSource), source))
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown date source.");
        }

        if (!Enum.IsDefined(typeof(DatePlacement), placement))
        {
            throw new ArgumentOutOfRangeException(nameof(placement), placement, "Unknown date placement.");
        }

        Format = format ?? throw new ArgumentNullException(nameof(format));
        Separator = separator ?? throw new ArgumentNullException(nameof(separator));

        var problem = Validate(format);
        if (problem != null)
        {
            throw new ArgumentException(problem, nameof(format));
        }

        Source = source;
        Placement = placement;
    }

    /// <summary>Which date is used.</summary>
    public DateSource Source { get; }

    /// <summary>The .NET custom date format; empty disables the rule.</summary>
    public string Format { get; }

    /// <summary>Where the date goes.</summary>
    public DatePlacement Placement { get; }

    /// <summary>Between the date and the base name; may be empty.</summary>
    public string Separator { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.AutoDate;

    /// <summary>False when the format is empty.</summary>
    public bool Enabled => Format.Length > 0;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (!Enabled)
        {
            return name;
        }

        var date = Source == DateSource.Created ? context.Item.Created : context.Item.Modified;
        if (date == default(DateTime))
        {
            throw new InvalidOperationException("The item has no date to use.");
        }

        var text = FormatDate(date, Format);
        if (text.Length == 0)
        {
            return name;
        }

        return Placement == DatePlacement.Prefix
            ? name.WithBaseName(text + Separator + name.BaseName)
            : name.WithBaseName(name.BaseName + Separator + text);
    }

    /// <summary>The text the rule adds for this date: "" for an empty format.</summary>
    /// <exception cref="ArgumentNullException">format is null.</exception>
    /// <exception cref="FormatException">.NET refuses the format, or it holds an unquoted K or z.</exception>
    public static string FormatDate(DateTime date, string format)
    {
        if (format is null)
        {
            throw new ArgumentNullException(nameof(format));
        }

        if (format.Length == 0)
        {
            return "";
        }

        if (HasTimeZoneLetter(format))
        {
            throw new FormatException(
                "The format holds a time-zone letter (K or z). Put it in quotes, as 'z', to add the letter itself.");
        }

        var custom = format.Length == 1 ? "%" + format : format;
        return date.ToString(custom, CultureInfo.InvariantCulture);
    }

    /// <summary>Null when the format is empty or usable; otherwise a non-empty message.</summary>
    /// <exception cref="ArgumentNullException">format is null.</exception>
    public static string? Validate(string format)
    {
        if (format is null)
        {
            throw new ArgumentNullException(nameof(format));
        }

        if (format.Length == 0)
        {
            return null;
        }

        try
        {
            FormatDate(Sample, format);
            return null;
        }
        catch (FormatException ex)
        {
            return ex.Message;
        }
    }

    private static bool HasTimeZoneLetter(string f)
    {
        var i = 0;
        while (i < f.Length)
        {
            var c = f[i];
            if (c == '\'' || c == '"')
            {
                i++;
                var closed = false;
                while (i < f.Length)
                {
                    if (f[i] == '\\')
                    {
                        i += 2;
                    }
                    else if (f[i] == c)
                    {
                        i++;
                        closed = true;
                        break;
                    }
                    else
                    {
                        i++;
                    }
                }

                if (!closed)
                {
                    return false;
                }
            }
            else if (c == '\\')
            {
                i += 2;
            }
            else if (c == 'K' || c == 'z')
            {
                return true;
            }
            else
            {
                i++;
            }
        }

        return false;
    }
}
