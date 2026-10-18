using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Disk.Tests;

/// <summary>The real disk, with hooks to record moves, change the disk mid-batch, fail a move or hide an entry.</summary>
internal sealed class HookedFileSystem : IFileSystem
{
    private readonly DiskFileSystem inner = new();

    /// <summary>Every Move call, forward and rollback, in call order (failed ones included).</summary>
    public List<(string From, string To)> Moves { get; } = [];

    /// <summary>The number of Exists and ExistsAny calls.</summary>
    public int Reads { get; private set; }

    /// <summary>Runs before the inner move: 1-based call number, from, to.</summary>
    public Action<int, string, string>? BeforeMove { get; set; }

    /// <summary>Returns an exception to throw instead of moving, or null to move.</summary>
    public Func<string, string, Exception?>? FailMove { get; set; }

    /// <summary>Makes Exists and ExistsAny return false for a path.</summary>
    public Func<string, bool>? HideFromExists { get; set; }

    public bool Exists(string path, ItemKind kind)
    {
        Reads++;
        return HideFromExists?.Invoke(path) != true && inner.Exists(path, kind);
    }

    public bool ExistsAny(string path)
    {
        Reads++;
        return HideFromExists?.Invoke(path) != true && inner.ExistsAny(path);
    }

    public void Move(string sourcePath, string targetPath, ItemKind kind)
    {
        Moves.Add((sourcePath, targetPath));
        BeforeMove?.Invoke(Moves.Count, sourcePath, targetPath);
        var failure = FailMove?.Invoke(sourcePath, targetPath);
        if (failure != null)
        {
            throw failure;
        }

        inner.Move(sourcePath, targetPath, kind);
    }
}
