using System;
using System.Collections.Generic;

namespace NameShift.Cmd;

/// <summary>One option group of <c>apply</c>: owns its words (constants), its state and its after-loop checks. A new
/// group is a new reader added to <see cref="ApplyReaders"/>; the parse loop does not change.</summary>
internal abstract class ApplyOptionReader
{
    /// <summary>The words this reader claims, in the order they appear in <c>ApplyOptionWords</c>.</summary>
    public abstract IReadOnlyList<string> Words { get; }

    /// <summary>Offers <c>args[index]</c>: <see cref="OptionRead.NotMine"/> when it is none of <see cref="Words"/>
    /// (ordinal, ignoring case), else <see cref="ReadOwn"/>.</summary>
    public OptionRead Read(IReadOnlyList<string> args, ref int index)
    {
        string arg = args[index];
        foreach (string own in Words)
        {
            if (string.Equals(arg, own, StringComparison.OrdinalIgnoreCase))
            {
                return ReadOwn(args, ref index, own);
            }
        }

        return OptionRead.NotMine;
    }

    /// <summary>Reads its own word <c>args[index]</c> (<paramref name="word"/> is the claimed word) and the values after
    /// it; the index ends on the last value consumed.</summary>
    protected abstract OptionRead ReadOwn(IReadOnlyList<string> args, ref int index, string word);

    /// <summary>After the loop: the cross-checks (a usage error, or null), then puts what was read into the draft.</summary>
    public abstract ParsedCommand? Finish(ApplyDraft draft);
}
