using System;
using NameShift.Disk;

namespace NameShift.Cmd.Exe;

/// <summary>NameShiftCmd.exe: the command line's only place that names the real history folder (the window's is
/// NameShift.App's Main). Every step is in <see cref="CommandRunner"/>.</summary>
internal static class Program
{
    private static int Main(string[] args) =>
        CommandRunner.ForDisk(Console.Out, Console.Error, Environment.CurrentDirectory, JournaledRenamer.DefaultHistoryRoot).Run(args);
}
