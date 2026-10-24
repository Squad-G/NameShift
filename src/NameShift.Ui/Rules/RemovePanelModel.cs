using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Remove panel; BuildRule maps them to a Core <see cref="RemoveGroupRule"/> of the four
/// Remove rules (position, text, character types, tidy).</summary>
public sealed class RemovePanelModel : RulePanelModel
{
    private string _firstText = "";
    private string _lastText = "";
    private string _fromText = "";
    private string _toText = "";
    private string _characters = "";
    private string _text = "";
    private bool _wholeWord;
    private bool _matchCase;
    private string _cropBefore = "";
    private string _cropAfter = "";
    private bool _keepMarker;
    private bool _digits;
    private bool _symbols;
    private bool _accents;
    private bool _highCharacters;
    private bool _trim;
    private bool _leadingDots;
    private bool _doubleSpaces;

    public RemovePanelModel()
        : base(RuleStage.Remove, Strings.RemoveTitle)
    {
    }

    /// <summary>The count of first characters to remove, as typed.</summary>
    public string FirstText
    {
        get => _firstText;
        set => SetField(ref _firstText, value ?? "");
    }

    /// <summary>The count of last characters to remove, as typed.</summary>
    public string LastText
    {
        get => _lastText;
        set => SetField(ref _lastText, value ?? "");
    }

    /// <summary>The start of the range to remove, as typed.</summary>
    public string FromText
    {
        get => _fromText;
        set => SetField(ref _fromText, value ?? "");
    }

    /// <summary>The end of the range to remove, as typed.</summary>
    public string ToText
    {
        get => _toText;
        set => SetField(ref _toText, value ?? "");
    }

    /// <summary>Characters to remove wherever they occur; passed as typed.</summary>
    public string Characters
    {
        get => _characters;
        set => SetField(ref _characters, value ?? "");
    }

    /// <summary>Text to remove; passed as typed.</summary>
    public string Text
    {
        get => _text;
        set => SetField(ref _text, value ?? "");
    }

    public bool WholeWord
    {
        get => _wholeWord;
        set => SetField(ref _wholeWord, value);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set => SetField(ref _matchCase, value);
    }

    public string CropBefore
    {
        get => _cropBefore;
        set => SetField(ref _cropBefore, value ?? "");
    }

    public string CropAfter
    {
        get => _cropAfter;
        set => SetField(ref _cropAfter, value ?? "");
    }

    public bool KeepMarker
    {
        get => _keepMarker;
        set => SetField(ref _keepMarker, value);
    }

    public bool Digits
    {
        get => _digits;
        set => SetField(ref _digits, value);
    }

    public bool Symbols
    {
        get => _symbols;
        set => SetField(ref _symbols, value);
    }

    public bool Accents
    {
        get => _accents;
        set => SetField(ref _accents, value);
    }

    public bool HighCharacters
    {
        get => _highCharacters;
        set => SetField(ref _highCharacters, value);
    }

    public bool Trim
    {
        get => _trim;
        set => SetField(ref _trim, value);
    }

    public bool LeadingDots
    {
        get => _leadingDots;
        set => SetField(ref _leadingDots, value);
    }

    public bool DoubleSpaces
    {
        get => _doubleSpaces;
        set => SetField(ref _doubleSpaces, value);
    }

    /// <summary>The typed count, or null when it is empty or not a whole number from 1.</summary>
    public int? First => TextPosition.Parse(FirstText);

    public int? Last => TextPosition.Parse(LastText);

    public int? From => TextPosition.Parse(FromText);

    public int? To => TextPosition.Parse(ToText);

    /// <summary>True when the first count is typed and cannot be used.</summary>
    public bool FirstInvalid => IsTyped(FirstText) && First == null;

    public bool LastInvalid => IsTyped(LastText) && Last == null;

    /// <summary>True when something is typed in the range and the range cannot apply; From and To are marked together.</summary>
    public bool RangeInvalid => !RangeApplies && (IsTyped(FromText) || IsTyped(ToText));

    public bool FromInvalid => RangeInvalid;

    public bool ToInvalid => RangeInvalid;

    /// <summary>Whole word needs a text.</summary>
    public bool WholeWordApplies => Text.Length > 0;

    /// <summary>Match case needs something to match.</summary>
    public bool MatchCaseApplies =>
        Characters.Length > 0 || Text.Length > 0 || CropBefore.Length > 0 || CropAfter.Length > 0;

    /// <summary>Keep the marker needs a crop text.</summary>
    public bool KeepMarkerApplies => CropBefore.Length > 0 || CropAfter.Length > 0;

    public override bool IsDefault =>
        FirstText.Length == 0 && LastText.Length == 0 && FromText.Length == 0 && ToText.Length == 0
        && Characters.Length == 0 && Text.Length == 0 && !WholeWord && !MatchCase
        && CropBefore.Length == 0 && CropAfter.Length == 0 && !KeepMarker
        && !Digits && !Symbols && !Accents && !HighCharacters
        && !Trim && !LeadingDots && !DoubleSpaces;

    private bool RangeApplies => From is int f && To is int t && f <= t;

    /// <summary>The rule, or null when no part would do anything; an unusable position text just skips that part.</summary>
    public override IRenameRule? BuildRule()
    {
        // Fixed order: position first, because its counts refer to the name the user sees; text and character
        // types next; tidy last, because it cleans the spaces and dots the others leave behind.
        var parts = new List<IRenameRule>
        {
            new RemovePositionRule(First, Last, RangeApplies ? From : null, RangeApplies ? To : null),
            new RemoveTextRule(Characters, Text, WholeWord, MatchCase, CropBefore, CropAfter, KeepMarker),
            new RemoveClassesRule(Digits, Symbols, Accents, HighCharacters),
            new RemoveTidyRule(Trim, LeadingDots, DoubleSpaces),
        };
        parts.RemoveAll(p => !p.Enabled);
        return parts.Count == 0 ? null : new RemoveGroupRule(parts);
    }

    protected override void ResetSettings()
    {
        FirstText = "";
        LastText = "";
        FromText = "";
        ToText = "";
        Characters = "";
        Text = "";
        WholeWord = false;
        MatchCase = false;
        CropBefore = "";
        CropAfter = "";
        KeepMarker = false;
        Digits = false;
        Symbols = false;
        Accents = false;
        HighCharacters = false;
        Trim = false;
        LeadingDots = false;
        DoubleSpaces = false;
    }

    private static bool IsTyped(string s) => !string.IsNullOrWhiteSpace(s);
}
