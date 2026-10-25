using System.Collections.Generic;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads <c>--recurse</c>: the sub-folders are listed too, and their files renamed in place.</summary>
internal sealed class RecurseReader : ApplyOptionReader
{
    internal const string RecurseOption = "--recurse";

    private static readonly string[] OwnWords = { RecurseOption };

    private bool _recurse;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        bool ok = TakeSwitch(ref _recurse, RecurseOption, out ParsedCommand? error);
        return OptionRead.From(ok, error);
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        draft.Recurse = _recurse;
        return null;
    }
}
