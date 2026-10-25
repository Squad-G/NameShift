namespace NameShift.Cmd;

/// <summary>How much <c>apply</c> writes when it succeeds. Errors are written at every level.</summary>
internal enum OutputLevel
{
    /// <summary>What the first command line wrote: the dry run's changed files, the summary, "Nothing to rename.".</summary>
    Normal,

    /// <summary>Nothing on success; every error exactly as at <see cref="Normal"/>.</summary>
    Quiet,

    /// <summary>On success, also the renamed files and the unchanged files.</summary>
    Verbose,
}
