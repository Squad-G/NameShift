using System;
using NameShift.Core;

namespace NameShift.Disk.Tests;

/// <summary>One temp folder and one hooked disk per test; temp names are predictable (~ns-00000000-n).</summary>
public abstract class ExecutorTestBase : IDisposable
{
    internal const string Token = "00000000";

    internal ExecutorTestBase()
    {
        Temp = new TempFolder();
        Disk = new HookedFileSystem();
        Executor = new RenameExecutor(Disk, () => Token);
    }

    internal TempFolder Temp { get; }

    internal HookedFileSystem Disk { get; }

    internal RenameExecutor Executor { get; }

    /// <summary>The full path of the n-th temporary name in the temp folder.</summary>
    internal string TempName(int n) => Temp.Full($"~ns-{Token}-{n}");

    /// <summary>A rename in the temp folder (or in one of its sub-folders).</summary>
    internal PlannedRename Rename(string oldName, string newName, ItemKind kind = ItemKind.File, string? sub = null) =>
        new(sub == null ? Temp.Path : Temp.Full(sub), oldName, newName, kind);

    public void Dispose()
    {
        Temp.Dispose();
        GC.SuppressFinalize(this);
    }
}
