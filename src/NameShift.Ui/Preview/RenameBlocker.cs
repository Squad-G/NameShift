namespace NameShift.Ui.Preview;

/// <summary>Why Rename cannot run a plan; None when it can.</summary>
public enum RenameBlocker
{
    /// <summary>Rename can run: at least one name changes, and there is no conflict and no rule error.</summary>
    None,

    /// <summary>No name changes, and there is no conflict and no rule error.</summary>
    NothingToRename,

    /// <summary>At least one item has a conflict, and no rule failed.</summary>
    Conflicts,

    /// <summary>At least one rule failed, and no item has a conflict.</summary>
    RuleErrors,

    /// <summary>At least one item has a conflict and at least one rule failed.</summary>
    ConflictsAndRuleErrors,
}
