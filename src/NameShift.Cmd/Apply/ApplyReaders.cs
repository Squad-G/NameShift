using System.Collections.Generic;

namespace NameShift.Cmd;

/// <summary>The readers of one parse, fresh each time, in CHECK order (not word order): the cross-checks of
/// <see cref="ApplyOptionReader.Finish"/> run in this order.</summary>
internal static class ApplyReaders
{
    public static IReadOnlyList<ApplyOptionReader> Create() => new ApplyOptionReader[]
    {
        new ReplaceCaseReader(), new RunSwitchesReader(), new PresetReader(), new AddReader(), new NumberingReader(),
    };
}
