namespace NameShift.Cmd;

/// <summary>The exit codes of NameShiftCmd.exe, for scripts.</summary>
public enum ExitCode
{
    /// <summary>The batch was renamed, or there was nothing to rename, or a dry run listed the new names.</summary>
    Success = 0,

    /// <summary>A usage or input error (the command line, or the folder): nothing was listed or renamed.</summary>
    InputError = 1,

    /// <summary>At least one conflict or rule error: nothing was renamed, and each one was printed.</summary>
    Blocked = 2,

    /// <summary>Nothing was renamed because of the disk: the batch was refused or rolled back, or its history could not
    /// be written before it started.</summary>
    NotRenamed = 3,

    /// <summary>The user must look: items were left under other names, the names changed but the history for Revert
    /// could not be saved, or the renamer failed unexpectedly (the disk state is unknown).</summary>
    NeedsAttention = 4,
}
