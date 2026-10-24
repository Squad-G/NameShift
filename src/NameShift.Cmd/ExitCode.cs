namespace NameShift.Cmd;

/// <summary>The exit codes of NameShiftCmd.exe, for scripts. <c>Strings.ExitCode0</c> to <c>ExitCode4</c> say the same
/// in the help.</summary>
public enum ExitCode
{
    /// <summary>Done, or there was nothing to rename (a dry run that listed the new names is done too).</summary>
    Success = 0,

    /// <summary>A usage or input error (the command line, or the folder): nothing was listed or renamed.</summary>
    InputError = 1,

    /// <summary>Conflicts or rule errors: nothing was renamed, and each one was printed.</summary>
    Blocked = 2,

    /// <summary>Nothing was renamed: the batch was refused or rolled back, its history could not be written before it
    /// started, or an unexpected error happened before any file was touched.</summary>
    NotRenamed = 3,

    /// <summary>Look at the disk: items were left under other names, the names changed but the history for Revert could
    /// not be saved, or an unexpected error happened during the rename (the disk state is unknown).</summary>
    NeedsAttention = 4,
}
