using System;
using System.Collections.Generic;
using System.Linq;

namespace NameShift.Disk;

/// <summary>What a Revert would do now, read from the journal and the disk without changing anything: the entries it
/// would skip, with why.</summary>
public sealed class RevertCheck
{
    internal RevertCheck(IReadOnlyList<SkippedRevert> skipped, bool anyRevertable)
    {
        var skippedList = (skipped ?? throw new ArgumentNullException(nameof(skipped))).ToArray();
        if (skippedList.Any(skip => skip == null))
        {
            throw new ArgumentException("A null skipped entry.", nameof(skipped));
        }

        Skipped = Array.AsReadOnly(skippedList);
        AnyModified = skippedList.Any(skip => skip.Reason == RevertSkipReason.Modified);
        AnyRevertable = anyRevertable;
    }

    /// <summary>Nothing to revert and nothing skipped: no journal, or one that is reverted, interrupted or unreadable.</summary>
    public static RevertCheck Empty { get; } = new(Array.Empty<SkippedRevert>(), anyRevertable: false);

    /// <summary>The entries a default Revert would skip, in journal order, each with one reason.</summary>
    public IReadOnlyList<SkippedRevert> Skipped { get; }

    /// <summary>True when at least one entry is skipped because its file was changed after the batch.</summary>
    public bool AnyModified { get; }

    /// <summary>True when a default Revert would revert at least one entry.</summary>
    public bool AnyRevertable { get; }
}
