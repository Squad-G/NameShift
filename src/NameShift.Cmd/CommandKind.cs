namespace NameShift.Cmd;

/// <summary>What a command line asks for.</summary>
internal enum CommandKind
{
    /// <summary>The main help.</summary>
    Help,

    /// <summary>The help of <c>apply</c>.</summary>
    ApplyHelp,

    /// <summary>The help of <c>undo</c>.</summary>
    UndoHelp,

    /// <summary>The version line.</summary>
    Version,

    /// <summary>Rename the files of a folder.</summary>
    Apply,

    /// <summary>Take back the last batch.</summary>
    Undo,

    /// <summary>A usage error: one line for the error stream.</summary>
    Error,
}
