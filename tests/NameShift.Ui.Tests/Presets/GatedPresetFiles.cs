using System.Collections.Generic;
using System.Threading;
using NameShift.Ui.Presets;
using NameShift.Ui.Tests.Files;

namespace NameShift.Ui.Tests.Presets;

/// <summary>The real disk files of a preset store, except that listing the folder can be held by a gate (and is
/// counted), so a test can look at the menu while the store is still reading.</summary>
internal sealed class GatedPresetFiles : IPresetFiles
{
    private readonly DiskPresetFiles _files = new DiskPresetFiles();
    private int _listCalls;

    /// <summary>When set, <see cref="ListJsonFiles"/> blocks on it first.</summary>
    public TestGate? Gate { get; set; }

    /// <summary>How many times the folder was listed.</summary>
    public int ListCalls => Volatile.Read(ref _listCalls);

    public bool FolderExists(string folder) => _files.FolderExists(folder);

    public IReadOnlyList<string> ListJsonFiles(string folder)
    {
        Interlocked.Increment(ref _listCalls);
        Gate?.Block();
        return _files.ListJsonFiles(folder);
    }

    public bool EntryExists(string path) => _files.EntryExists(path);

    public long FileLength(string path) => _files.FileLength(path);

    public byte[] ReadAll(string path) => _files.ReadAll(path);

    public void WriteAtomic(string path, byte[] content) => _files.WriteAtomic(path, content);

    public void Delete(string path) => _files.Delete(path);
}
