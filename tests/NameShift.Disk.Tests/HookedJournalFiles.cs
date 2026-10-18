using System;
using System.Collections.Generic;
using System.IO;

namespace NameShift.Disk.Tests;

/// <summary>The real journal files, recording every call by file name, with hooks to fail a write or a delete.</summary>
internal sealed class HookedJournalFiles : IJournalFiles
{
    private readonly DiskJournalFiles inner = new();

    /// <summary>The number of Exists and ReadAll calls.</summary>
    public int Reads { get; private set; }

    /// <summary>The file names of every WriteAtomic call, failed ones included.</summary>
    public List<string> Writes { get; } = [];

    /// <summary>The file names of every Delete call, failed ones included.</summary>
    public List<string> Deletes { get; } = [];

    /// <summary>Returns an exception to throw instead of writing the file (by name), or null to write.</summary>
    public Func<string, Exception?>? FailWrite { get; set; }

    /// <summary>Returns an exception to throw instead of deleting the file (by name), or null to delete.</summary>
    public Func<string, Exception?>? FailDelete { get; set; }

    public bool Exists(string path)
    {
        Reads++;
        return inner.Exists(path);
    }

    public byte[] ReadAll(string path)
    {
        Reads++;
        return inner.ReadAll(path);
    }

    public void WriteAtomic(string path, byte[] content)
    {
        var name = Path.GetFileName(path);
        Writes.Add(name);
        var failure = FailWrite?.Invoke(name);
        if (failure != null)
        {
            throw failure;
        }

        inner.WriteAtomic(path, content);
    }

    public void Delete(string path)
    {
        var name = Path.GetFileName(path);
        Deletes.Add(name);
        var failure = FailDelete?.Invoke(name);
        if (failure != null)
        {
            throw failure;
        }

        inner.Delete(path);
    }
}
