using System.Collections.Generic;

namespace NameShift.Core;

/// <summary>One conflict check run on the pipeline's results (NS-5 invalid names, NS-6 duplicates, NS-7 path length).
/// A check is pure: whatever it needs from the disk is given to its constructor by the app.</summary>
public interface IConflictCheck
{
    /// <summary>Returns, for each result in the same order, the conflicts this check finds (an empty list when none).
    /// The returned list has exactly one entry per result.</summary>
    IReadOnlyList<IReadOnlyList<Conflict>> Check(IReadOnlyList<RenameResult> results);
}
