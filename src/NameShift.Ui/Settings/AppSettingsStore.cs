using System;
using System.IO;
using System.Threading.Tasks;
using NameShift.Disk;
using NameShift.Ui.Presets;

namespace NameShift.Ui.Settings;

/// <summary>
/// Reads and writes the two files the window keeps between runs (NS-37), and only those two, in its own folder:
/// <c>settings.json</c> (<see cref="AppSettingsJson"/>) and <c>last-rules.json</c> (the rules in the preset format,
/// <see cref="PresetJson"/>). Reads are lenient and never write; saves are atomic (through the preset store's file
/// seam) and silent. Nothing here throws on the disk: a start or a close must never fail on these files. The files stay
/// on this machine; nothing is sent anywhere.
/// </summary>
public sealed class AppSettingsStore
{
    /// <summary>The preset name the last rules are saved under.</summary>
    public const string LastRulesName = "(last used)";

    /// <summary>How long the window waits at close for the save.</summary>
    public static readonly TimeSpan SaveLimit = TimeSpan.FromSeconds(2);

    internal const string SettingsFileName = "settings.json";
    internal const string RulesFileName = "last-rules.json";

    private readonly IPresetFiles _files;

    /// <summary>A store over the real disk.</summary>
    /// <exception cref="ArgumentException">The folder is null, empty or white space, or not a valid path.</exception>
    public AppSettingsStore(string folder)
        : this(folder, new DiskPresetFiles())
    {
    }

    /// <summary>A store over the given file access (tests).</summary>
    internal AppSettingsStore(string folder, IPresetFiles files)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            throw new ArgumentException("The folder is required.", nameof(folder));
        }

        _files = files ?? throw new ArgumentNullException(nameof(files));
        try
        {
            string full = Path.GetFullPath(folder);
            string root = Path.GetPathRoot(full) ?? "";
            Folder = full.Length > root.Length
                ? full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : full;
        }
        catch (Exception e) when (e is NotSupportedException || e is PathTooLongException || e is ArgumentException)
        {
            throw new ArgumentException("The folder is not a valid path.", nameof(folder), e);
        }
    }

    /// <summary>%LOCALAPPDATA%\NameShift: per machine, since a window position and a last folder belong to one screen
    /// layout. Only computed, never created here; the first save creates it. Without a usable local application data
    /// folder it is %TEMP%\NameShift (<see cref="AppDataFolders.Resolve"/>).</summary>
    public static string DefaultFolder =>
        FolderFor(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Path.GetTempPath());

    /// <summary>The settings folder for a local application data path and a temp path (the testable part of
    /// <see cref="DefaultFolder"/>).</summary>
    internal static string FolderFor(string? localAppData, string tempPath) => AppDataFolders.Resolve(localAppData, tempPath);

    /// <summary>The store's folder, a full path without a trailing separator.</summary>
    public string Folder { get; }

    private string SettingsPath => Path.Combine(Folder, SettingsFileName);

    private string RulesPath => Path.Combine(Folder, RulesFileName);

    /// <summary>The saved window facts, or <see cref="AppSettings.Empty"/> when the file is missing, over 1 MB, corrupt
    /// or of a newer version (the file is then left as it is). Never throws.</summary>
    public AppSettings LoadSettings()
    {
        byte[]? content = ReadSmall(SettingsPath, AppSettingsJson.MaxContentLength);
        return content == null ? AppSettings.Empty : AppSettingsJson.Read(content);
    }

    /// <summary>The last rules, or null when the file is missing, over 1 MB, unreadable or of a newer version (the
    /// file is then left as it is). Never throws.</summary>
    public Preset? LoadRules()
    {
        byte[]? content = ReadSmall(RulesPath, PresetJson.MaxContentLength);
        if (content == null)
        {
            return null;
        }

        try
        {
            PresetRead read = PresetJson.Read(content);
            return read.Status == PresetReadStatus.Ok ? read.Preset : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Writes both files, each atomically and on its own (one failure does not stop the other). Creates the
    /// folder when it is missing. Silent: a failure leaves the old file and no temporary file.</summary>
    /// <exception cref="ArgumentNullException">The settings or the rules are null.</exception>
    public void Save(AppSettings settings, Preset rules)
    {
        RejectNulls(settings, rules);

        try
        {
            _files.WriteAtomic(SettingsPath, AppSettingsJson.Serialize(settings));
        }
        catch (Exception)
        {
            // Silent: closing the window is not a place for a message, and the rules are still written.
        }

        try
        {
            _files.WriteAtomic(RulesPath, PresetJson.Serialize(rules));
        }
        catch (Exception)
        {
            // Silent, as above.
        }
    }

    /// <summary>
    /// Runs <see cref="Save"/> on the pool and blocks the calling thread for at most <paramref name="limit"/>: true when
    /// the save finished in time. A late save keeps running on the pool; it may finish, or be cut when the process
    /// ends, and the atomic write then leaves the old file or the new one, never half a file.
    /// </summary>
    /// <exception cref="ArgumentNullException">The settings or the rules are null.</exception>
    public bool SaveWithin(AppSettings settings, Preset rules, TimeSpan limit)
    {
        RejectNulls(settings, rules);

        Task task = Task.Run(() => Save(settings, rules));
        try
        {
            return task.Wait(limit);
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The file's bytes, or null when it is missing, larger than the limit (then it is not read) or fails.</summary>
    private byte[]? ReadSmall(string path, int maxLength)
    {
        try
        {
            if (!_files.EntryExists(path) || _files.FileLength(path) > maxLength)
            {
                return null;
            }

            return _files.ReadAll(path);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static void RejectNulls(AppSettings settings, Preset rules)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        if (rules == null)
        {
            throw new ArgumentNullException(nameof(rules));
        }
    }
}
