using System;
using System.IO;

namespace NameShift.Disk;

/// <summary>The real disk for the undo journal. Only the journal store calls it, with paths of its own files inside the
/// history root.</summary>
internal sealed class DiskJournalFiles : IJournalFiles
{
    /// <inheritdoc />
    public bool Exists(string path) => File.Exists(path);

    /// <inheritdoc />
    public byte[] ReadAll(string path) => File.ReadAllBytes(path);

    /// <summary>Writes "&lt;name&gt;.&lt;32 hex&gt;.tmp" in the same folder (created if needed), flushed to the disk,
    /// then replaces the file with it (or moves it into place when there is no file yet).</summary>
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
    public void Delete(string path)
    {
        // File.Delete ignores a missing file but throws on a missing folder.
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

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
            // Best effort: a leftover temp file is harmless (it is never read), and this cleanup must not hide the
            // write's own exception.
        }
    }
}
