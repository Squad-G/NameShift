using System;
using System.Collections.Generic;
using System.Globalization;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Auto date panel; BuildRule maps them to the Core <see cref="AutoDateRule"/>.</summary>
/// <remarks>The format is held as typed. A format that .NET or the rule refuses is reported by
/// <see cref="FormatError"/> and makes BuildRule return null, so the date is skipped until the format is fixed. The
/// defaults are a usable setting, so an enabled panel with default settings builds a rule.</remarks>
public sealed class AutoDatePanelModel : RulePanelModel
{
    private const string DefaultFormat = "yyyy-MM-dd";
    private const string DefaultSeparator = "_";

    /// <summary>The fixed date the example is shown for (Kind unspecified: the rule refuses time-zone letters, so the example never depends on the time zone of the PC).</summary>
    public static readonly DateTime SampleDate = new DateTime(2024, 3, 9, 14, 5, 7);

    private static readonly DateSource[] SourceList = { DateSource.Modified, DateSource.Created, DateSource.Taken };

    private static readonly DatePlacement[] PlacementList = { DatePlacement.Prefix, DatePlacement.Suffix };

    private static readonly string[] CommonFormatList =
    {
        "yyyy-MM-dd", "yyyyMMdd", "yyyy-MM-dd_HH-mm-ss", "yyyyMMdd_HHmmss", "dd-MM-yyyy", "yyyy-MM", "yyyy",
    };

    private DateSource _source = DateSource.Modified;
    private string _format = DefaultFormat;
    private DatePlacement _placement = DatePlacement.Prefix;
    private string _separator = DefaultSeparator;

    public AutoDatePanelModel()
        : base(RuleStage.AutoDate, Strings.AutoDateTitle)
    {
    }

    /// <summary>The list order: Modified, Created, Taken (Photo date).</summary>
    public static IReadOnlyList<DateSource> Sources => SourceList;

    /// <summary>The list order: Prefix, Suffix.</summary>
    public static IReadOnlyList<DatePlacement> Placements => PlacementList;

    /// <summary>The formats offered in the drop-down; every one is usable and gives a name Windows accepts.</summary>
    public static IReadOnlyList<string> CommonFormats => CommonFormatList;

    /// <summary>The source's position in <see cref="Sources"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(DateSource source) => Array.IndexOf(SourceList, source);

    /// <summary>The placement's position in <see cref="Placements"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(DatePlacement placement) => Array.IndexOf(PlacementList, placement);

    /// <exception cref="ArgumentOutOfRangeException">The source is not a defined value (a programmer error).</exception>
    public DateSource Source
    {
        get => _source;
        set
        {
            if (Array.IndexOf(SourceList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown date source.");
            }

            SetField(ref _source, value);
        }
    }

    /// <summary>The .NET date format as typed; never trimmed.</summary>
    public string Format
    {
        get => _format;
        set => SetField(ref _format, value ?? "");
    }

    /// <exception cref="ArgumentOutOfRangeException">The placement is not a defined value (a programmer error).</exception>
    public DatePlacement Placement
    {
        get => _placement;
        set
        {
            if (Array.IndexOf(PlacementList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown date placement.");
            }

            SetField(ref _placement, value);
        }
    }

    /// <summary>The text put between the date and the name; never trimmed.</summary>
    public string Separator
    {
        get => _separator;
        set => SetField(ref _separator, value ?? "");
    }

    /// <summary>The message for a format the rule refuses, or "" when it is empty or fine. Worked out on each read, so it is live.</summary>
    public string FormatError
    {
        get
        {
            if (Format.Length == 0)
            {
                return "";
            }

            string? message = AutoDateRule.Validate(Format);
            return message == null ? "" : string.Format(CultureInfo.CurrentCulture, Strings.AutoDateFormatInvalid, message);
        }
    }

    /// <summary>The sample date in the typed format, or "" when the format is empty or refused.</summary>
    public string Example =>
        Format.Length == 0 || AutoDateRule.Validate(Format) != null ? "" : AutoDateRule.FormatDate(SampleDate, Format);

    /// <summary>The example label's text, or "" when there is no example (the control then hides the label).</summary>
    public string ExampleText =>
        Example.Length == 0 ? "" : string.Format(CultureInfo.CurrentCulture, Strings.AutoDateExample, Example);

    public override bool IsDefault =>
        Source == DateSource.Modified && Format == DefaultFormat && Placement == DatePlacement.Prefix
        && Separator == DefaultSeparator;

    /// <summary>The rule, or null while the format is empty or refused (the date is then skipped).</summary>
    public override IRenameRule? BuildRule()
    {
        if (Format.Length == 0 || AutoDateRule.Validate(Format) != null)
        {
            return null;
        }

        return new AutoDateRule(Source, Format, Placement, Separator);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, string> SaveSettings() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(Source)] = Source.ToString(),
        [nameof(Format)] = Format,
        [nameof(Placement)] = Placement.ToString(),
        [nameof(Separator)] = Separator,
    };

    /// <inheritdoc />
    protected override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        if (TryGetEnum(settings, nameof(Source), out DateSource source) && IndexOf(source) >= 0)
        {
            Source = source;
        }

        if (TryGetText(settings, nameof(Format), out string format))
        {
            Format = format;
        }

        if (TryGetEnum(settings, nameof(Placement), out DatePlacement placement) && IndexOf(placement) >= 0)
        {
            Placement = placement;
        }

        if (TryGetText(settings, nameof(Separator), out string separator))
        {
            Separator = separator;
        }
    }

    protected override void ResetSettings()
    {
        Source = DateSource.Modified;
        Format = DefaultFormat;
        Placement = DatePlacement.Prefix;
        Separator = DefaultSeparator;
    }
}
