using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Disk.Tests;

/// <summary>An in-memory set of paths with their kinds, for planner tests; it cannot move anything.</summary>
internal sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, ItemKind> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileFingerprint> fingerprints = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The number of Exists and ExistsAny calls.</summary>
    public int Reads { get; private set; }

    /// <summary>Adds an entry.</summary>
    public FakeFileSystem With(string path, ItemKind kind = ItemKind.File)
    {
        entries[path] = kind;
        return this;
    }

    public bool Exists(string path, ItemKind kind)
    {
        Reads++;
        return entries.TryGetValue(path, out var found) && found == kind;
    }

    public bool ExistsAny(string path)
    {
        Reads++;
        return entries.ContainsKey(path);
    }

    /// <summary>The number of TryGetFingerprint calls.</summary>
    public int FingerprintReads { get; private set; }

    /// <summary>Sets what TryGetFingerprint returns for a path (null by default: unreadable).</summary>
    public FakeFileSystem WithFingerprint(string path, FileFingerprint fingerprint)
    {
        fingerprints[path] = fingerprint;
        return this;
    }

    public FileFingerprint? TryGetFingerprint(string path)
    {
        FingerprintReads++;
        return fingerprints.TryGetValue(path, out var found) ? found : null;
    }

    public void Move(string sourcePath, string targetPath, ItemKind kind) =>
        throw new NotSupportedException("The planner never moves anything.");
}
