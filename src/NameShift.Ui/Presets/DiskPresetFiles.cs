using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NameShift.Ui.Presets;

/// <summary>The real disk for the preset store. Only the store calls it, with its own folder and paths of its own files
/// inside it (the same pattern as the journal's DiskJournalFiles in NameShift.Disk).</summary>
internal sealed class DiskPresetFiles : IPresetFiles
{
    /// <summary>How old a leftover temporary file must be before a save deletes it: a save in progress in another window
    /// ends in milliseconds, so it is never touched.</summary>
    internal static readonly TimeSpan StaleAge = TimeSpan.FromHours(1);

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
    /// deleted quietly whatever happens. Before writing, it deletes this file's own leftovers of a cut-off save (see
    /// <see cref="DeleteStaleTemps"/>).</summary>
    public void WriteAtomic(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        DeleteStaleTemps(path);
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

    /// <summary>Best effort, never throws: deletes, in the file's own folder only (never below it), the files named
    /// exactly "&lt;stem&gt;.&lt;32 lower-case hex&gt;.tmp" (the name <see cref="WriteAtomic"/> gives its temporary file,
    /// same stem as <paramref name="path"/>) last written over <see cref="StaleAge"/> ago. A file that cannot be read or
    /// deleted is skipped. It runs before this save's own temporary file exists.</summary>
    internal static void DeleteStaleTemps(string path)
    {
        try
        {
            var folder = Path.GetDirectoryName(path)!;
            var stem = Path.GetFileName(Path.ChangeExtension(path, null));
            var ownTemp = new Regex("^" + Regex.Escape(stem) + @"\.[0-9a-f]{32}\.tmp$", RegexOptions.CultureInvariant);
            var cutoff = DateTime.UtcNow - StaleAge;

            // The wildcard can also match other names through an 8.3 short name, so each name is checked again.
            foreach (var file in Directory.EnumerateFiles(folder, stem + ".*.tmp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (ownTemp.IsMatch(Path.GetFileName(file)) && File.GetLastWriteTimeUtc(file) < cutoff)
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception)
                {
                    // Open, read-only or gone: skip it; the next save tries again.
                }
            }
        }
        catch (Exception)
        {
            // A missing or unreadable folder: nothing to clean, and the save goes on.
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
            // Best effort: a leftover temp file is harmless (it is never listed, it does not end in ".json"), and this
            // cleanup must not hide the write's own exception.
        }
    }
}
