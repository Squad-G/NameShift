using System;
using System.Collections.Generic;
using System.Globalization;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the RegEx panel; BuildRule maps them to the Core <see cref="RegexRule"/>.</summary>
/// <remarks>The pattern is held as typed. A pattern that does not parse is reported by <see cref="PatternError"/> and
/// makes BuildRule return null, so the rule is skipped until the pattern is fixed.</remarks>
public sealed class RegexPanelModel : RulePanelModel
{
    private string _pattern = "";
    private string _replacement = "";
    private bool _ignoreCase;
    private bool _includeExtension;

    public RegexPanelModel()
        : base(RuleStage.RegEx, Strings.RegexTitle)
    {
    }

    /// <summary>The regular expression; never trimmed.</summary>
    public string Pattern
    {
        get => _pattern;
        set => SetField(ref _pattern, value ?? "");
    }

    /// <summary>The replacement text ($1 and ${name} refer to groups); never trimmed.</summary>
    public string Replacement
    {
        get => _replacement;
        set => SetField(ref _replacement, value ?? "");
    }

    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => SetField(ref _ignoreCase, value);
    }

    public bool IncludeExtension
    {
        get => _includeExtension;
        set => SetField(ref _includeExtension, value);
    }

    /// <summary>The message for a pattern that does not parse, or "" when it is empty or fine. Worked out on each read, so it is live.</summary>
    public string PatternError
    {
        get
        {
            if (Pattern.Length == 0)
            {
                return "";
            }

            string? message = RegexRule.Validate(Pattern);
            return message == null ? "" : string.Format(CultureInfo.CurrentCulture, Strings.RegexPatternInvalid, message);
        }
    }

    public override bool IsDefault =>
        Pattern.Length == 0 && Replacement.Length == 0 && !IgnoreCase && !IncludeExtension;

    /// <summary>The rule, or null while the pattern is empty or does not parse (the rule is then skipped).</summary>
    public override IRenameRule? BuildRule()
    {
        if (Pattern.Length == 0 || RegexRule.Validate(Pattern) != null)
        {
            return null;
        }

        return new RegexRule(Pattern, Replacement, IgnoreCase, IncludeExtension);
    }

    /// <inheritdoc />
    public override IReadOnlyDictionary<string, string> SaveSettings() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [nameof(Pattern)] = Pattern,
        [nameof(Replacement)] = Replacement,
        [nameof(IgnoreCase)] = BoolText(IgnoreCase),
        [nameof(IncludeExtension)] = BoolText(IncludeExtension),
    };

    /// <inheritdoc />
    protected override void ApplySettings(IReadOnlyDictionary<string, string> settings)
    {
        if (TryGetText(settings, nameof(Pattern), out string pattern))
        {
            Pattern = pattern;
        }

        if (TryGetText(settings, nameof(Replacement), out string replacement))
        {
            Replacement = replacement;
        }

        if (TryGetBool(settings, nameof(IgnoreCase), out bool ignoreCase))
        {
            IgnoreCase = ignoreCase;
        }

        if (TryGetBool(settings, nameof(IncludeExtension), out bool includeExtension))
        {
            IncludeExtension = includeExtension;
        }
    }

    protected override void ResetSettings()
    {
        Pattern = "";
        Replacement = "";
        IgnoreCase = false;
        IncludeExtension = false;
    }
}
