using System;
using System.Collections.Generic;

namespace NameShift.Core;

/// <summary>Flags new names whose full path (folder, separator, new name) is over 259 characters (NS-7).</summary>
public sealed class PathLengthCheck : IConflictCheck
{
    /// <summary>The longest full path Windows programs accept without long-path support: MAX_PATH (260) minus the terminator.</summary>
    private const int MaxPathLength = 259;

    private static readonly IReadOnlyList<Conflict> None = Array.Empty<Conflict>();

    /// <summary>Checks the changed results; unchanged and error results will not be renamed, so they are not checked.</summary>
    public IReadOnlyList<IReadOnlyList<Conflict>> Check(IReadOnlyList<RenameResult> results)
    {
        if (results == null)
        {
            throw new ArgumentNullException(nameof(results));
        }

        var found = new IReadOnlyList<Conflict>[results.Count];
        for (var i = 0; i < found.Length; i++)
        {
            var result = results[i];
            found[i] = result.Status == RenameStatus.Changed && FullPathLength(result) > MaxPathLength
                ? [new Conflict(ConflictCode.PathTooLong)]
                : None;
        }

        return found;
    }

    /// <summary>Counts the full new path without building it: Path.Combine throws on .NET Framework for characters NS-5 flags.</summary>
    private static int FullPathLength(RenameResult result)
    {
        var folder = result.Item.FolderPath;
        var endsWithSeparator = folder.Length > 0 && (folder[folder.Length - 1] == '\\' || folder[folder.Length - 1] == '/');
        return folder.Length + (endsWithSeparator ? 0 : 1) + result.NewName.Length;
    }
}