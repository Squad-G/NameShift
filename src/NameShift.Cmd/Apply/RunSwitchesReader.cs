using System.Collections.Generic;
using static NameShift.Cmd.OptionArgs;

namespace NameShift.Cmd;

/// <summary>Reads <c>--dry-run</c>, <c>--quiet|-q</c> and <c>--verbose|-v</c>.</summary>
internal sealed class RunSwitchesReader : ApplyOptionReader
{
    internal const string DryRunOption = "--dry-run";
    internal const string QuietOption = "--quiet";
    internal const string QuietShortOption = "-q";
    internal const string VerboseOption = "--verbose";
    internal const string VerboseShortOption = "-v";

    private static readonly string[] OwnWords =
        { DryRunOption, QuietOption, QuietShortOption, VerboseOption, VerboseShortOption };

    private bool _dryRun;
    private bool _quiet;
    private bool _verbose;

    public override IReadOnlyList<string> Words => OwnWords;

    protected override OptionRead ReadOwn(IReadOnlyList<string> args, ref int i, string word)
    {
        if (Is(word, DryRunOption))
        {
            _dryRun = true;
        }
        else if (Is(word, QuietOption) || Is(word, QuietShortOption))
        {
            if (_quiet)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, QuietOption)));
            }

            _quiet = true;
        }
        else
        {
            if (_verbose)
            {
                return OptionRead.Failed(Fail(Format(Strings.RepeatedOption, VerboseOption)));
            }

            _verbose = true;
        }

        return OptionRead.Taken;
    }

    public override ParsedCommand? Finish(ApplyDraft draft)
    {
        // A conflict is reported before a missing folder or rule: it is the clearer mistake. --verbose wins the tie.
        if (_quiet && _verbose)
        {
            return Fail(Strings.QuietWithVerbose);
        }

        if (_quiet && _dryRun)
        {
            return Fail(Strings.QuietWithDryRun);
        }

        draft.DryRun = _dryRun;
        draft.Output = _quiet ? OutputLevel.Quiet : _verbose ? OutputLevel.Verbose : OutputLevel.Normal;
        return null;
    }
}
