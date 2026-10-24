using System;

namespace NameShift.Core;

/// <summary>Changes the last extension (as split by <see cref="NameParts"/>); folders are never touched. Invariant casing.</summary>
public sealed class ExtensionRule : IRenameRule
{
    /// <summary>Creates the rule; <paramref name="value"/> is used by Fixed and Extra only.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="ExtensionMode"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    public ExtensionRule(ExtensionMode mode, string value = "")
    {
        if (!Enum.IsDefined(typeof(ExtensionMode), mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "Not a defined extension mode.");
        }

        Mode = mode;
        Value = Clean(value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>What the rule does to the extension.</summary>
    public ExtensionMode Mode { get; }

    /// <summary>The cleaned value used by Fixed and Extra: white space and dots trimmed at both ends, inner dots kept;
    /// "" when only dots and spaces were typed.</summary>
    public string Value { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Extension;

    /// <summary>False for Same, and for Fixed or Extra with an empty <see cref="Value"/>.</summary>
    public bool Enabled
    {
        get
        {
            switch (Mode)
            {
                case ExtensionMode.Same: return false;
                case ExtensionMode.Fixed:
                case ExtensionMode.Extra: return Value.Length > 0;
                default: return true;
            }
        }
    }

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (context.Item.Kind == ItemKind.Folder)
        {
            return name;
        }

        switch (Mode)
        {
            case ExtensionMode.Lower:
                return name.WithExtension(name.Extension.ToLowerInvariant());
            case ExtensionMode.Upper:
                return name.WithExtension(name.Extension.ToUpperInvariant());
            case ExtensionMode.Fixed:
                return Value.Length == 0 ? name : name.WithExtension(Value);
            case ExtensionMode.Extra:
                return Value.Length == 0 ? name : new NameParts(name.ToFullName(), Value);
            case ExtensionMode.Remove:
                return new NameParts(name.BaseName, "");
            default:
                return name;
        }
    }

    private static string Clean(string text)
    {
        var start = 0;
        var end = text.Length;
        while (start < end && IsTrimmed(text[start]))
        {
            start++;
        }

        while (end > start && IsTrimmed(text[end - 1]))
        {
            end--;
        }

        return text.Substring(start, end - start);
    }

    private static bool IsTrimmed(char c) => c == '.' || char.IsWhiteSpace(c);
}
