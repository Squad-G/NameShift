namespace NameShift.Cmd;

/// <summary>What a command line asks for.</summary>
internal enum CommandKind
{
    /// <summary>The main help.</summary>
    Help,

    /// <summary>The help of <c>apply</c>.</summary>
    ApplyHelp,

    /// <summary>The version line.</summary>
    Version,

    /// <summary>Rename the files of a folder.</summary>
    Apply,

    /// <summary>A usage error: one line for the error stream.</summary>
    Error,
}
