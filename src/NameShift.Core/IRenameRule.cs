namespace NameShift.Core;

/// <summary>One step of the rename pipeline. A rule is immutable: the window builds a new one when a setting changes.</summary>
public interface IRenameRule
{
    /// <summary>Where the rule runs in the fixed pipeline order (docs/PLAN.md).</summary>
    RuleStage Stage { get; }

    /// <summary>False when the rule is switched off or has nothing to do; the pipeline then skips it.</summary>
    bool Enabled { get; }

    /// <summary>Returns the new (base name, extension) from the one left by the previous rule.
    /// Throws (any exception, message in plain words) when it cannot compute a name for this item.</summary>
    NameParts Apply(NameParts name, ItemContext context);
}
