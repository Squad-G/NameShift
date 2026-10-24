using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NameShift.Ui.Presets;

/// <summary>The real disk for the preset store. Only the store calls it, with its own folder and paths of its own files
/// inside it (the same pattern as the journal's DiskJournalFiles in NameShift.Disk).</summary>
internal sealed class DiskPresetFiles : IPresetFiles
{
    /// <summary>For tests: called with the temporary file's path after it is flushed and before it replaces or is
    /// moved onto the preset file. A test makes it throw to fail the write.</summary>
    internal Action<string>? BeforeReplace { get; set; }

    /// <inheritdoc />
    public bool FolderExists(string folder) => Directory.Exists(folder);

    /// <inheritdoc />
    public IReadOnlyList<string> ListJsonFiles(string folder) =>
        OnlyJson(Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly));

    /// <inheritdoc />
    public bool EntryExists(string path) => File.Exists(path) || Directory.Exists(path);

    /// <inheritdoc />
    public long FileLength(string path) => new FileInfo(path).Length;

    /// <inheritdoc />
    public byte[] ReadAll(string path) => File.ReadAllBytes(path);

    /// <summary>Writes "&lt;name&gt;.&lt;32 hex&gt;.tmp" in the same folder (created if needed), flushed to the disk,
    /// then replaces the file with it (or moves it into place when there is no file yet). The temporary file is
    /// deleted quietly whatever happens.</summary>
    public void WriteAtomic(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{Path.ChangeExtension(path, null)}.{Guid.NewGuid():N}.tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(content, 0, content.Length);
                stream.Flush(flushToDisk: true);
            }

            BeforeReplace?.Invoke(tempPath);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            DeleteTempQuietly(tempPath);
        }
    }

    /// <inheritdoc />
    public void Delete(string path) => File.Delete(path);

    /// <summary>The paths whose names end in ".json", ignoring case. The "*.json" pattern can also match a longer
    /// extension through an 8.3 short name ("x.jsonx"), so the listing is filtered again.</summary>
    internal static IReadOnlyList<string> OnlyJson(IEnumerable<string> paths) =>
        paths.Where(path => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ToList();

    private static void DeleteTempQuietly(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception)
        {
            // Best effort: a leftover temp file is harmless (it is never listed, it does not end in ".json"), and this
            // cleanup must not hide the write's own exception.
        }
    }
}
