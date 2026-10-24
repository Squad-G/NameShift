using System;
using System.Collections.Generic;
using System.Globalization;

namespace NameShift.Core;

/// <summary>Replaces or reshapes the base name; the extension is never touched.</summary>
public sealed class NameRule : IRenameRule
{
    /// <summary>Creates the rule; <paramref name="value"/> is used by Fixed only.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="NameMode"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public NameRule(NameMode mode, string value = "")
    {
        if (!Enum.IsDefined(typeof(NameMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined name mode.");
        }

        Mode = mode;
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>What the rule does to the base name.</summary>
    public NameMode Mode { get; }

    /// <summary>The new base name for Fixed, exactly as typed (never trimmed or validated: the checks flag bad names; white space only counts as empty).</summary>
    public string Value { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Name;

    /// <summary>False for Keep, and for Fixed with an empty or white-space-only <see cref="Value"/>.</summary>
    public bool Enabled
    {
        get
        {
            switch (Mode)
            {
                case NameMode.Keep: return false;
                case NameMode.Fixed: return !string.IsNullOrWhiteSpace(Value);
                default: return true;
            }
        }
    }

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        switch (Mode)
        {
            // Enabled is false with an empty or white-space-only Value and the pipeline skips the rule; Apply checks again because it is
            // public and may be called directly.
            case NameMode.Fixed:
                return string.IsNullOrWhiteSpace(Value) ? name : name.WithBaseName(Value);
            case NameMode.Remove:
                return name.WithBaseName("");
            case NameMode.Reverse:
                return name.WithBaseName(ReverseTextElements(name.BaseName));
            default:
                return name;
        }
    }

    private static string ReverseTextElements(string text)
    {
        var elements = new List<string>();
        var e = StringInfo.GetTextElementEnumerator(text);
        while (e.MoveNext())
        {
            elements.Add(e.GetTextElement());
        }

        elements.Reverse();
        return string.Concat(elements);
    }
}
