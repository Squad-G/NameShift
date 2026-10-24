using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Append folder name panel; BuildRule maps them to the Core <see cref="FolderNameRule"/>.</summary>
/// <remarks>The level count is held as typed. An unusable count makes BuildRule return null, so the folder name is
/// skipped until it is fixed. The defaults are a usable setting, so an enabled panel with default settings builds a
/// rule.</remarks>
public sealed class FolderNamePanelModel : RulePanelModel
{
    private const string DefaultLevelsText = "1";
    private const string DefaultSeparator = "_";

    private static readonly FolderNamePlacement[] PlacementList =
    {
        FolderNamePlacement.Prefix, FolderNamePlacement.Suffix,
    };

    private FolderNamePlacement _placement = FolderNamePlacement.Prefix;
    private string _levelsText = DefaultLevelsText;
    private string _separator = DefaultSeparator;

    public FolderNamePanelModel()
        : base(RuleStage.FolderName, Strings.FolderNameTitle)
    {
    }

    /// <summary>The list order: Prefix, Suffix.</summary>
    public static IReadOnlyList<FolderNamePlacement> Placements => PlacementList;

    /// <summary>The placement's position in <see cref="Placements"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(FolderNamePlacement placement) => Array.IndexOf(PlacementList, placement);

    /// <exception cref="ArgumentOutOfRangeException">The placement is not a defined value (a programmer error).</exception>
    public FolderNamePlacement Placement
    {
        get => _placement;
        set
        {
            if (Array.IndexOf(PlacementList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown folder name placement.");
            }

            SetField(ref _placement, value);
        }
    }

    /// <summary>The number of folder levels as typed.</summary>
    public string LevelsText
    {
        get => _levelsText;
        set => SetField(ref _levelsText, value ?? "");
    }

    /// <summary>The text put between the folder names and the name; never trimmed.</summary>
    public string Separator
    {
        get => _separator;
        set => SetField(ref _separator, value ?? "");
    }

    /// <summary>The typed level count, or null when it is empty or not a whole number from 1.</summary>
    public int? Levels => TextPosition.Parse(LevelsText);

    /// <summary>True when the count cannot be used; an empty box is marked too, because the field has a default.</summary>
    public bool LevelsInvalid => Levels == null;

    public override bool IsDefault =>
        Placement == FolderNamePlacement.Prefix && LevelsText == DefaultLevelsText && Separator == DefaultSeparator;

    /// <summary>The rule, or null while the level count is unusable.</summary>
    public override IRenameRule? BuildRule() =>
        Levels is int levels ? new FolderNameRule(Placement, levels, Separator) : null;

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, string> SaveSettings() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(Placement)] = Placement.ToString(),
        [nameof(LevelsText)] = LevelsText,
        [nameof(Separator)] = Separator,
    };

    /// <inheritdoc />
    protected override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        if (TryGetEnum(settings, nameof(Placement), out FolderNamePlacement placement) && IndexOf(placement) >= 0)
        {
            Placement = placement;
        }

        if (TryGetText(settings, nameof(LevelsText), out string levelsText))
        {
            LevelsText = levelsText;
        }

        if (TryGetText(settings, nameof(Separator), out string separator))
        {
            Separator = separator;
        }
    }

    protected override void ResetSettings()
    {
        Placement = FolderNamePlacement.Prefix;
        LevelsText = DefaultLevelsText;
        Separator = DefaultSeparator;
    }
}
