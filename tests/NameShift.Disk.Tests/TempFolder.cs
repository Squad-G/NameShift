using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NameShift.Disk.Tests;

/// <summary>A fresh folder under %TEMP%\NameShift.Disk.Tests for one test; the only place tests write.</summary>
internal sealed class TempFolder : IDisposable
{
    private static readonly string Root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "NameShift.Disk.Tests");

    public TempFolder()
    {
        Path = System.IO.Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>The folder's full path.</summary>
    public string Path { get; }

    /// <summary>Creates a file whose content is its own name unless given, so identity survives renames.</summary>
    public string File(string name, string? content = null)
    {
        var path = System.IO.Path.Combine(Path, name);
        System.IO.File.WriteAllText(path, content ?? System.IO.Path.GetFileName(name));
        return path;
    }

    /// <summary>Creates a folder.</summary>
    public string Folder(string name)
    {
        var path = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>The full path of a relative entry.</summary>
    public string Full(string relative) => System.IO.Path.Combine(Path, relative);

    /// <summary>The entries' names with their exact on-disk case, sorted ordinal.</summary>
    public IReadOnlyList<string> Listing(string? sub = null) =>
        Directory.GetFileSystemEntries(sub == null ? Path : System.IO.Path.Combine(Path, sub))
            .Select(entry => System.IO.Path.GetFileName(entry))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>The text of a file.</summary>
    public string Content(string relative) => System.IO.File.ReadAllText(System.IO.Path.Combine(Path, relative));

    public void Dispose()
    {
        if (!Path.StartsWith(Root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Refusing to delete a folder outside the test root: {Path}");
        }

        if (!Directory.Exists(Path))
        {
            return;
        }

        const FileAttributes Blocking = FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System;
        foreach (var entry in Directory.GetFileSystemEntries(Path, "*", SearchOption.AllDirectories))
        {
            System.IO.File.SetAttributes(entry, System.IO.File.GetAttributes(entry) & ~Blocking);
        }

        Directory.Delete(Path, recursive: true);
    }
}
