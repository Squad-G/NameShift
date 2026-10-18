using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Add panel; BuildRule maps them to the Core <see cref="AddRule"/>.</summary>
public sealed class AddPanelModel : RulePanelModel
{
    private string _prefix = "";
    private string _insert = "";
    private string _insertPositionText = "";
    private bool _insertFromEnd;
    private string _suffix = "";
    private bool _wordSpace;

    public AddPanelModel()
        : base(RuleStage.Add, Strings.AddTitle)
    {
    }

    public string Prefix
    {
        get => _prefix;
        set => SetField(ref _prefix, value ?? "");
    }

    public string Insert
    {
        get => _insert;
        set => SetField(ref _insert, value ?? "");
    }

    /// <summary>The position as typed.</summary>
    public string InsertPositionText
    {
        get => _insertPositionText;
        set => SetField(ref _insertPositionText, value ?? "");
    }

    public bool InsertFromEnd
    {
        get => _insertFromEnd;
        set => SetField(ref _insertFromEnd, value);
    }

    public string Suffix
    {
        get => _suffix;
        set => SetField(ref _suffix, value ?? "");
    }

    public bool WordSpace
    {
        get => _wordSpace;
        set => SetField(ref _wordSpace, value);
    }

    /// <summary>The typed position, or null when it is empty or not a whole number from 1.</summary>
    public int? InsertPosition => TextPosition.Parse(InsertPositionText);

    /// <summary>True when the position cannot be used and the user has asked for an insert or typed something.</summary>
    public bool InsertPositionInvalid =>
        InsertPosition == null && (Insert.Length > 0 || !string.IsNullOrWhiteSpace(InsertPositionText));

    /// <summary>The position field's label: it says how the number counts.</summary>
    public string InsertPositionLabel => InsertFromEnd ? Strings.AddPositionFromEnd : Strings.AddPosition;

    public override bool IsDefault =>
        Prefix.Length == 0 && Insert.Length == 0 && InsertPositionText.Length == 0 && !InsertFromEnd
        && Suffix.Length == 0 && !WordSpace;

    /// <summary>The rule, or null when it would do nothing; an unusable position just skips the insert part.</summary>
    public override IRenameRule? BuildRule()
    {
        var rule = new AddRule(Prefix, Insert, InsertPosition, InsertFromEnd, Suffix, WordSpace);
        return rule.Enabled ? rule : null;
    }

    protected override void ResetSettings()
    {
        Prefix = "";
        Insert = "";
        InsertPositionText = "";
        InsertFromEnd = false;
        Suffix = "";
        WordSpace = false;
    }
}
