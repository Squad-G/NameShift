using System;

namespace NameShift.Core;

/// <summary>Why a rule could not compute a name.</summary>
public sealed class RenameError
{
    /// <summary>Creates an error for the rule that failed.</summary>
    public RenameError(RuleStage stage, string ruleName, string message)
    {
        Stage = stage;
        RuleName = ruleName ?? throw new ArgumentNullException(nameof(ruleName));
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    /// <summary>The failing rule's stage.</summary>
    public RuleStage Stage { get; }

    /// <summary>The failing rule's type name, e.g. "NumberingRule".</summary>
    public string RuleName { get; }

    /// <summary>The exception's message.</summary>
    public string Message { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Stage}: {Message}";
}
