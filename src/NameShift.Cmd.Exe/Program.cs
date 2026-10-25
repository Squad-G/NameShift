using System;
using NameShift.Disk;
using NameShift.Ui.Presets;

namespace NameShift.Cmd.Exe;

/// <summary>NameShiftCmd.exe: the command line's only place that names the real history and presets folders (the
/// window's are NameShift.App's Main). Every step is in <see cref="CommandRunner"/>; <see cref="ConsoleEntry"/> sets the console up for
/// UTF-8 around it.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        string version = ProductVersion.Of(typeof(Program).Assembly);
        return ConsoleEntry.Run(
            new SystemConsoleEncoding(),
            () => Console.Out,
            () => Console.Error,
            (output, error) => CommandRunner.ForDisk(
                output, error, Environment.CurrentDirectory, JournaledRenamer.DefaultHistoryRoot, PresetStore.DefaultFolder,
                version).Run(args));
    }
}
