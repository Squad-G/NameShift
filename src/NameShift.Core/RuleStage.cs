namespace NameShift.Core;

/// <summary>The fixed pipeline order of docs/PLAN.md; the numeric value is the stage number.</summary>
public enum RuleStage
{
    /// <summary>Stage 1.</summary>
    RegEx = 1,

    /// <summary>Stage 2.</summary>
    Name = 2,

    /// <summary>Stage 3.</summary>
    Replace = 3,

    /// <summary>Stage 4.</summary>
    Case = 4,

    /// <summary>Stage 5.</summary>
    Remove = 5,

    /// <summary>Stage 6.</summary>
    MoveCopy = 6,

    /// <summary>Stage 7.</summary>
    Add = 7,

    /// <summary>Stage 8.</summary>
    AutoDate = 8,

    /// <summary>Stage 9.</summary>
    Numbering = 9,

    /// <summary>Stage 10.</summary>
    FolderName = 10,

    /// <summary>Stage 11.</summary>
    Extension = 11,
}
