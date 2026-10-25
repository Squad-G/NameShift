using System.Collections.Generic;
using System.Threading;
using NameShift.Ui.Presets;
using NameShift.Ui.Tests.Files;

namespace NameShift.Ui.Tests.Settings;

/// <summary>The real disk files of a settings store (in a temp folder), except that a write can be held by a gate, and
/// reads and writes are counted, so a test can play a slow disk or check that a file was not read.</summary>
internal sealed class BlockingSettingsFiles : IPresetFiles
{
    private readonly DiskPresetFiles _files = new DiskPresetFiles();
    private int _reads;
    private int _writes;

    /// <summary>When set, <see cref="WriteAtomic"/> blocks on it first.</summary>
    public TestGate? Gate { get; set; }

    /// <summary>How many files were read.</summary>
    public int Reads => Volatile.Read(ref _reads);

    /// <summary>How many writes finished.</summary>
    public int Writes => Volatile.Read(ref _writes);

    public bool FolderExists(string folder) => _files.FolderExists(folder);

    public IReadOnlyList<string> ListJsonFiles(string folder) => _files.ListJsonFiles(folder);

    public bool EntryExists(string path) => _files.EntryExists(path);

    public long FileLength(string path) => _files.FileLength(path);

    public byte[] ReadAll(string path)
    {
        Interlocked.Increment(ref _reads);
        return _files.ReadAll(path);
    }

    public void WriteAtomic(string path, byte[] content)
    {
        Gate?.Block();
        _files.WriteAtomic(path, content);
        Interlocked.Increment(ref _writes);
    }

    public void Delete(string path) => _files.Delete(path);
}
