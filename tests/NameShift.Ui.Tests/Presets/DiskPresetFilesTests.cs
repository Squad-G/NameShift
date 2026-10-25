using System;
using System.IO;
using System.Linq;
using System.Text;
using NameShift.Ui.Presets;
using Xunit;

namespace NameShift.Ui.Tests.Presets;

/// <summary>NS-91 AC 3, 4: WriteAtomic deletes its own leftover temporary files (same stem, 32 lower-case hex digits,
/// over an hour old) and nothing else, best effort. Only in a fresh temp folder, deleted after each test.</summary>
public sealed class DiskPresetFilesTests : IDisposable
{
    private const string Hex = "0123456789abcdef0123456789abcdef";

    private readonly string _root;
    private readonly string _folder;

    public DiskPresetFilesTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "NameShift.DiskPresetFilesTests." + Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_root, "NameShift");
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        foreach (string file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    private string SettingsPath => Path.Combine(_folder, "settings.json");

    private static byte[] Utf8(string text) => new UTF8Encoding(false).GetBytes(text);

    private string Make(string name, bool old)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, name);
        File.SetLastWriteTimeUtc(path, old ? DateTime.UtcNow.AddHours(-2) : DateTime.UtcNow);
        return path;
    }

    private string[] Names() =>
        Directory.GetFiles(_folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

    [Fact]
    public void WriteAtomic_DeletesOnlyOwnStaleTemps()
    {
        Make("settings.json", old: true);
        Make("settings.txt", old: true);
        Make("settings.abc.tmp", old: true);
        Make("other." + Hex + ".tmp", old: true);
        Make("settings." + Hex.Replace('0', '1') + ".tmp", old: false);
        Make("settings." + Hex + ".tmp", old: true);

        new DiskPresetFiles().WriteAtomic(SettingsPath, Utf8("new"));

        Assert.Equal(
            new[] { "other." + Hex + ".tmp", "settings." + Hex.Replace('0', '1') + ".tmp", "settings.abc.tmp", "settings.json", "settings.txt" },
            Names());
        Assert.Equal("new", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void WriteAtomic_DottedStem_UsesTheWholeStem()
    {
        string path = Path.Combine(_folder, "My.Preset.json");
        Make("My.Preset." + Hex + ".tmp", old: true);
        Make("My." + Hex + ".tmp", old: true);

        new DiskPresetFiles().WriteAtomic(path, Utf8("p"));

        Assert.Equal(new[] { "My." + Hex + ".tmp", "My.Preset.json" }, Names());
    }

    [Fact]
    public void WriteAtomic_UpperCaseHex_Kept()
    {
        string upper = "settings." + Hex.ToUpperInvariant() + ".tmp";
        Make(upper, old: true);

        new DiskPresetFiles().WriteAtomic(SettingsPath, Utf8("new"));

        Assert.Equal(new[] { upper, "settings.json" }, Names());
    }

    [Fact]
    public void WriteAtomic_LockedOrReadOnly_SkippedAndWritten()
    {
        string readOnly = Make("settings." + Hex + ".tmp", old: true);
        File.SetAttributes(readOnly, FileAttributes.ReadOnly);
        try
        {
            new DiskPresetFiles().WriteAtomic(SettingsPath, Utf8("first"));

            Assert.True(File.Exists(readOnly));
            Assert.Equal("first", File.ReadAllText(SettingsPath));
        }
        finally
        {
            File.SetAttributes(readOnly, FileAttributes.Normal);
        }

        string locked = Make("settings." + Hex.Replace('0', '2') + ".tmp", old: true);
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            new DiskPresetFiles().WriteAtomic(SettingsPath, Utf8("second"));

            Assert.True(File.Exists(locked));
            Assert.Equal("second", File.ReadAllText(SettingsPath));
        }
    }

    [Fact]
    public void WriteAtomic_FolderMissing_NotAnError()
    {
        string path = Path.Combine(_root, "missing", "sub", "settings.json");

        new DiskPresetFiles().WriteAtomic(path, Utf8("new"));

        Assert.Equal("new", File.ReadAllText(path));
        Assert.Equal(new[] { "settings.json" }, Directory.GetFiles(Path.GetDirectoryName(path)!).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void WriteAtomic_CutOffSaveLeftover_RemovedByNextSave()
    {
        File.WriteAllText(SettingsPath, "saved");
        string leftover = Path.Combine(_folder, "settings." + Guid.NewGuid().ToString("N") + ".tmp");
        File.WriteAllText(leftover, "{\"cut\": \"off before the replace\"}");
        File.SetLastWriteTimeUtc(leftover, DateTime.UtcNow - DiskPresetFiles.StaleAge - TimeSpan.FromMinutes(1));

        new DiskPresetFiles().WriteAtomic(SettingsPath, Utf8("next"));

        Assert.False(File.Exists(leftover));
        Assert.Equal(new[] { "settings.json" }, Names());
        Assert.Equal("next", File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void WriteAtomic_NoSubfolderTouched()
    {
        string sub = Path.Combine(_folder, "sub");
        Directory.CreateDirectory(sub);
        string inner = Path.Combine(sub, "settings." + Hex + ".tmp");
        File.WriteAllText(inner, "x");
        File.SetLastWriteTimeUtc(inner, DateTime.UtcNow.AddHours(-2));

        new DiskPresetFiles().WriteAtomic(SettingsPath, Utf8("new"));

        Assert.True(File.Exists(inner));
    }

    [Fact]
    public void StaleAge_IsOneHour() => Assert.Equal(TimeSpan.FromHours(1), DiskPresetFiles.StaleAge);
}
