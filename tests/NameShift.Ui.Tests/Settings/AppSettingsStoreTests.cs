using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using NameShift.Core;
using NameShift.Ui.Presets;
using NameShift.Ui.Rules;
using NameShift.Ui.Settings;
using NameShift.Ui.Tests.Files;
using Xunit;

namespace NameShift.Ui.Tests.Settings;

/// <summary>NS-37 AC 1 to 4, 10, 11: the settings store, against real temp folders (never the user's
/// local application data folder).</summary>
public sealed class AppSettingsStoreTests : IDisposable
{
    private static readonly string Folder = @"D:\" + "\u00c9t\u00e9";

    private readonly string _root;
    private readonly string _folder;

    public AppSettingsStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "ns-settings-" + Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_root, "NameShift");
        Directory.CreateDirectory(_root);
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

    private string RulesPath => Path.Combine(_folder, "last-rules.json");

    private AppSettingsStore Store() => new AppSettingsStore(_folder);

    private static AppSettings EveryValue() =>
        new AppSettings(new SavedWindow(-1200, 30, 900, 600, true), 300, Folder);

    private static Preset OneRule() => new Preset(AppSettingsStore.LastRulesName, new[]
    {
        new PresetPanel(RuleStage.Replace, true, new Dictionary<string, string> { ["Find"] = "a" }),
    });

    private string WriteRaw(string path, string text)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        return path;
    }

    private AppSettings LoadRaw(string json)
    {
        WriteRaw(SettingsPath, json);
        return Store().LoadSettings();
    }

    private static void AssertEmpty(AppSettings settings)
    {
        Assert.Null(settings.Window);
        Assert.Null(settings.MainSplit);
        Assert.Null(settings.LastFolder);
    }

    private static void AssertSame(AppSettings expected, AppSettings actual)
    {
        Assert.Equal(expected.MainSplit, actual.MainSplit);
        Assert.Equal(expected.LastFolder, actual.LastFolder);
        if (expected.Window == null)
        {
            Assert.Null(actual.Window);
            return;
        }

        Assert.NotNull(actual.Window);
        Assert.Equal(expected.Window.Left, actual.Window!.Left);
        Assert.Equal(expected.Window.Top, actual.Window.Top);
        Assert.Equal(expected.Window.Width, actual.Window.Width);
        Assert.Equal(expected.Window.Height, actual.Window.Height);
        Assert.Equal(expected.Window.Maximized, actual.Window.Maximized);
    }

    // AC 1

    [Fact]
    public void RoundTrip_EveryValue()
    {
        AppSettingsStore store = Store();

        store.Save(EveryValue(), OneRule());

        AssertSame(EveryValue(), store.LoadSettings());
        AssertSame(EveryValue(), new AppSettingsStore(_folder).LoadSettings());
    }

    [Fact]
    public void Save_CreatesTheFolder_Utf8NoBom_Indented()
    {
        Assert.False(Directory.Exists(_folder));

        Store().Save(EveryValue(), OneRule());

        Assert.True(File.Exists(SettingsPath));
        byte[] bytes = File.ReadAllBytes(SettingsPath);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        string text = Encoding.UTF8.GetString(bytes);
        Assert.Contains("\n", text);
        Assert.Contains("\"version\": 1", text);
        Assert.Contains("\"window\": {", text);
        Assert.Contains("\"maximized\": true", text);
        Assert.Contains("\"mainSplit\": 300", text);
    }

    [Fact]
    public void NullValues_Omitted()
    {
        Store().Save(AppSettings.Empty, OneRule());

        string text = File.ReadAllText(SettingsPath);
        Assert.Contains("\"version\": 1", text);
        Assert.DoesNotContain("window", text);
        Assert.DoesNotContain("mainSplit", text);
        Assert.DoesNotContain("lastFolder", text);
    }

    [Fact]
    public void FailedWrite_KeepsTheOldFile_NoTemp()
    {
        Store().Save(EveryValue(), OneRule());
        byte[] oldSettings = File.ReadAllBytes(SettingsPath);
        byte[] oldRules = File.ReadAllBytes(RulesPath);
        var failing = new AppSettingsStore(_folder, new DiskPresetFiles { BeforeReplace = _ => throw new IOException("disk") });

        failing.Save(new AppSettings(null, 10, @"C:\Other"), new Preset(AppSettingsStore.LastRulesName, Array.Empty<PresetPanel>()));

        Assert.Equal(oldSettings, File.ReadAllBytes(SettingsPath));
        Assert.Equal(oldRules, File.ReadAllBytes(RulesPath));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void OneFileFails_TheOtherIsWritten()
    {
        var files = new DiskPresetFiles
        {
            BeforeReplace = temp =>
            {
                if (Path.GetFileName(temp).StartsWith("settings.", StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("disk");
                }
            },
        };

        new AppSettingsStore(_folder, files).Save(EveryValue(), OneRule());

        Assert.False(File.Exists(SettingsPath));
        Assert.True(File.Exists(RulesPath));
        Assert.Equal("a", Assert.Single(Store().LoadRules()!.Panels).Settings["Find"]);
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    // AC 2

    [Fact]
    public void Missing_Defaults()
    {
        AssertEmpty(Store().LoadSettings());
        Assert.Null(Store().LoadRules());
        Assert.False(Directory.Exists(_folder));

        Directory.CreateDirectory(_folder);
        AssertEmpty(Store().LoadSettings());
        Assert.Null(Store().LoadRules());
        Assert.Empty(Directory.GetFileSystemEntries(_folder));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[1,2]")]
    [InlineData("\"x\"")]
    [InlineData("random")]
    public void Corrupt_Defaults_FileLeftAlone(string content)
    {
        Directory.CreateDirectory(_folder);
        byte[] bytes;
        if (content == "random")
        {
            bytes = new byte[512];
            new Random(37).NextBytes(bytes);
        }
        else
        {
            bytes = new UTF8Encoding(false).GetBytes(content);
        }

        File.WriteAllBytes(SettingsPath, bytes);

        AssertEmpty(Store().LoadSettings());
        Assert.Equal(bytes, File.ReadAllBytes(SettingsPath));
    }

    [Fact]
    public void TooLarge_Defaults_NotRead()
    {
        Directory.CreateDirectory(_folder);
        string json = "{\"mainSplit\": 300}";
        var bytes = new byte[(1024 * 1024) + 1];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)' ';
        }

        Encoding.ASCII.GetBytes(json).CopyTo(bytes, 0);
        File.WriteAllBytes(SettingsPath, bytes);
        var files = new BlockingSettingsFiles();

        AssertEmpty(new AppSettingsStore(_folder, files).LoadSettings());

        Assert.Equal(0, files.Reads);
    }

    [Fact]
    public void NewerVersion_Defaults()
    {
        string json = "{\"version\": 2, \"mainSplit\": 300}";
        WriteRaw(SettingsPath, json);
        byte[] before = File.ReadAllBytes(SettingsPath);

        AssertEmpty(Store().LoadSettings());

        Assert.Equal(before, File.ReadAllBytes(SettingsPath));
    }

    [Fact]
    public void MissingVersion_ReadsAsOne()
    {
        Assert.Equal(300, LoadRaw("{\"mainSplit\": 300}").MainSplit);
    }

    [Fact]
    public void BadValue_TextForANumber_DefaultsAlone()
    {
        AppSettings settings = LoadRaw("{\"version\": 1, \"mainSplit\": \"abc\", \"lastFolder\": \"D:\\\\Pics\"}");

        Assert.Null(settings.MainSplit);
        Assert.Equal(@"D:\Pics", settings.LastFolder);
    }

    [Theory]
    [InlineData("\"mainSplit\": -5")]
    [InlineData("\"mainSplit\": 1.5")]
    [InlineData("\"mainSplit\": true")]
    [InlineData("\"mainSplit\": null")]
    [InlineData("\"mainSplit\": {}")]
    [InlineData("\"mainSplit\": 99999999999")]
    public void BadValue_MainSplit_DefaultsAlone(string member)
    {
        AppSettings settings = LoadRaw(
            "{\"version\": 1, " + member + ", \"lastFolder\": \"D:\\\\Pics\", "
            + "\"window\": {\"left\": 1, \"top\": 2, \"width\": 900, \"height\": 600}}");

        Assert.Null(settings.MainSplit);
        Assert.Equal(@"D:\Pics", settings.LastFolder);
        Assert.NotNull(settings.Window);
    }

    [Theory]
    [InlineData("\"width\": 0")]
    [InlineData("\"width\": -900")]
    [InlineData("\"width\": \"wide\"")]
    public void BadValue_WindowSize_NoWindow_OthersLoad(string width)
    {
        AppSettings settings = LoadRaw(
            "{\"version\": 1, \"window\": {\"left\": 1, \"top\": 2, " + width + ", \"height\": 600}, \"mainSplit\": 300}");

        Assert.Null(settings.Window);
        Assert.Equal(300, settings.MainSplit);
    }

    [Theory]
    [InlineData("\"yes\"")]
    [InlineData("1")]
    [InlineData("\"True\"")]
    [InlineData("null")]
    public void BadValue_Maximized_False_WindowKept(string maximized)
    {
        AppSettings settings = LoadRaw(
            "{\"window\": {\"left\": 1, \"top\": 2, \"width\": 900, \"height\": 600, \"maximized\": " + maximized + "}}");

        Assert.NotNull(settings.Window);
        Assert.False(settings.Window!.Maximized);
        Assert.Equal(900, settings.Window.Width);
    }

    [Theory]
    [InlineData("42")]
    [InlineData("\"  \"")]
    [InlineData("\"\"")]
    [InlineData("true")]
    [InlineData("[\"D:\\\\Pics\"]")]
    public void BadValue_LastFolder_Null(string folder)
    {
        AppSettings settings = LoadRaw("{\"lastFolder\": " + folder + ", \"mainSplit\": 300}");

        Assert.Null(settings.LastFolder);
        Assert.Equal(300, settings.MainSplit);
    }

    [Theory]
    [InlineData("\"version\": \"abc\"")]
    [InlineData("\"version\": 1.5")]
    [InlineData("\"version\": -1")]
    public void BadVersion_Defaults(string version)
    {
        AssertEmpty(LoadRaw("{" + version + ", \"mainSplit\": 300}"));
    }

    [Fact]
    public void WindowNeedsAllFour()
    {
        AppSettings settings = LoadRaw("{\"window\": {\"top\": 2, \"width\": 900, \"height\": 600, \"maximized\": true}, \"mainSplit\": 300}");

        Assert.Null(settings.Window);
        Assert.Equal(300, settings.MainSplit);
    }

    [Fact]
    public void UnknownMembers_Ignored()
    {
        AppSettings settings = LoadRaw(
            "{\"version\": 1, \"theme\": \"dark\", \"rightSplit\": 250, \"panels\": [1, 2], \"mainSplit\": 300, "
            + "\"window\": {\"left\": -5, \"top\": 6, \"width\": 900, \"height\": 600, \"dpi\": 144}, \"lastFolder\": \"D:\\\\Pics\"}");

        Assert.Equal(300, settings.MainSplit);
        Assert.Equal(@"D:\Pics", settings.LastFolder);
        Assert.Equal(-5, settings.Window!.Left);
        Assert.Equal(6, settings.Window.Top);
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("tr-TR")]
    public void RoundTrip_GermanAndTurkishCultures(string culture)
    {
        CultureInfo oldCulture = CultureInfo.CurrentCulture;
        CultureInfo oldUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            CultureInfo.CurrentUICulture = new CultureInfo(culture);
            var settings = new AppSettings(WindowPlacement.ToSaved(-1650, 15, 1651, 1081, 1.5, maximized: true), 1234, Folder);

            Store().Save(settings, OneRule());

            AssertSame(settings, Store().LoadSettings());
            string text = File.ReadAllText(SettingsPath);
            Assert.DoesNotMatch(new Regex(@"\d[,.]\d"), text);
            Assert.Contains("\"maximized\": true", text);
            Assert.Contains("\"width\": 1101", text);
            Assert.Contains("\"mainSplit\": 1234", text);

            // A fraction stays invalid whatever the culture's separators.
            Assert.Null(LoadRaw("{\"mainSplit\": 1.5}").MainSplit);
            Assert.Null(LoadRaw("{\"mainSplit\": \"1,5\"}").MainSplit);
            Assert.Equal(-12, LoadRaw("{\"window\": {\"left\": -12, \"top\": 0, \"width\": 9, \"height\": 9}}").Window!.Left);
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUiCulture;
        }
    }

    // AC 3

    [Fact]
    public void Rules_RoundTrip_ApplyOnce()
    {
        var replace = new ReplacePanelModel { Find = "a", Enabled = true };
        var casePanel = new CasePanelModel();
        Store().Save(EveryValue(), Preset.FromPanels(AppSettingsStore.LastRulesName, new IRulePanelModel[] { replace, casePanel }));

        Preset? rules = Store().LoadRules();
        Assert.NotNull(rules);
        Assert.Equal(AppSettingsStore.LastRulesName, rules!.Name);
        var freshReplace = new ReplacePanelModel();
        var freshCase = new CasePanelModel { Mode = CaseMode.Upper, Enabled = true };
        var host = new RulePanelHostModel(new IRulePanelModel[] { freshCase, freshReplace });
        int changed = 0;
        host.RulesChanged += (_, _) => changed++;

        host.ApplyPreset(rules);

        Assert.Equal(1, changed);
        Assert.Equal(replace.SaveSettings(), freshReplace.SaveSettings());
        Assert.True(freshReplace.Enabled);
        Assert.Equal(casePanel.SaveSettings(), freshCase.SaveSettings());
        Assert.False(freshCase.Enabled);
    }

    [Fact]
    public void Rules_NoPanels_AllDefault()
    {
        Store().Save(EveryValue(), new Preset(AppSettingsStore.LastRulesName, Array.Empty<PresetPanel>()));
        var replace = new ReplacePanelModel { Find = "x", Enabled = true };
        var casePanel = new CasePanelModel { Mode = CaseMode.Lower };
        var host = new RulePanelHostModel(new IRulePanelModel[] { casePanel, replace });

        host.ApplyPreset(Store().LoadRules()!);

        Assert.All(host.Panels, panel =>
        {
            Assert.True(panel.IsDefault);
            Assert.False(panel.Enabled);
        });
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("newer")]
    [InlineData("large")]
    public void Rules_Unreadable_Null(string kind)
    {
        Directory.CreateDirectory(_folder);
        byte[] bytes = kind switch
        {
            "corrupt" => Encoding.UTF8.GetBytes("{\"version\": 1, \"name\": "),
            "newer" => Encoding.UTF8.GetBytes("{\"version\": 2, \"name\": \"(last used)\", \"panels\": []}"),
            _ => Enumerable.Repeat((byte)' ', (1024 * 1024) + 1).ToArray(),
        };
        File.WriteAllBytes(RulesPath, bytes);
        var files = new BlockingSettingsFiles();

        Assert.Null(new AppSettingsStore(_folder, files).LoadRules());

        Assert.Equal(bytes, File.ReadAllBytes(RulesPath));
        if (kind == "large")
        {
            Assert.Equal(0, files.Reads);
        }
    }

    // AC 4

    [Fact]
    public void SaveWithin_SlowDisk_ReturnsFalseInTime()
    {
        var files = new BlockingSettingsFiles();
        var store = new AppSettingsStore(_folder, files);
        using (var gate = new TestGate())
        {
            files.Gate = gate;
            var watch = Stopwatch.StartNew();
            try
            {
                bool done = store.SaveWithin(EveryValue(), OneRule(), TimeSpan.FromMilliseconds(200));

                watch.Stop();
                Assert.False(done);
                Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), "SaveWithin took " + watch.Elapsed);
                gate.WaitUntilReached();
                Assert.Equal(0, files.Writes);
            }
            finally
            {
                gate.Release();
            }

            // The late save still lands once the disk answers.
            Assert.True(SpinWait.SpinUntil(() => files.Writes == 2, TestWait.Timeout), "The late save never finished.");
        }

        AssertSame(EveryValue(), Store().LoadSettings());
        Assert.NotNull(Store().LoadRules());
    }

    [Fact]
    public void SaveWithin_Normal_True()
    {
        Assert.True(Store().SaveWithin(EveryValue(), OneRule(), TimeSpan.FromSeconds(30)));

        AssertSame(EveryValue(), Store().LoadSettings());
        Assert.NotNull(Store().LoadRules());
    }

    // AC 10

    [Fact]
    public void Reset_ThenSave_NoPanel()
    {
        var replace = new ReplacePanelModel();
        var casePanel = new CasePanelModel();
        var host = new RulePanelHostModel(new IRulePanelModel[] { casePanel, replace });
        host.ApplyPreset(Preset.FromPanels("Two", new IRulePanelModel[]
        {
            new ReplacePanelModel { Find = "a", Enabled = true },
            new CasePanelModel { Mode = CaseMode.Upper, Enabled = true },
        }));
        Assert.True(replace.Enabled && casePanel.Enabled);

        host.ResetAll();
        Store().Save(EveryValue(), Preset.FromPanels(AppSettingsStore.LastRulesName, host.Panels));

        Assert.Empty(Store().LoadRules()!.Panels);
    }

    // AC 11

    [Fact]
    public void OnlyTheStoreFolder_IsWritten()
    {
        Store().Save(EveryValue(), OneRule());

        Assert.Equal(new[] { _folder }, Directory.GetFileSystemEntries(_root));
        string[] names = Directory.GetFileSystemEntries(_folder, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;
        Assert.Equal(new[] { "last-rules.json", "settings.json" }, names);
    }

    // Arguments

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankFolder(string? folder)
    {
        Assert.Throws<ArgumentException>(() => new AppSettingsStore(folder!));
    }

    [Fact]
    public void Constructor_FullPath_NoTrailingSeparator()
    {
        Assert.Equal(_folder, new AppSettingsStore(_folder + Path.DirectorySeparatorChar).Folder);
    }

    [Fact]
    public void Save_RejectsNulls()
    {
        AppSettingsStore store = Store();

        Assert.Throws<ArgumentNullException>(() => store.Save(null!, OneRule()));
        Assert.Throws<ArgumentNullException>(() => store.Save(AppSettings.Empty, null!));
        Assert.Throws<ArgumentNullException>(() => store.SaveWithin(null!, OneRule(), TimeSpan.FromSeconds(1)));
        Assert.Throws<ArgumentNullException>(() => store.SaveWithin(AppSettings.Empty, null!, TimeSpan.FromSeconds(1)));
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void AppSettings_RejectsNegativeSplit_BlankFolderIsNull()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AppSettings(null, -1, null));
        Assert.Null(new AppSettings(null, 0, "  ").LastFolder);
        Assert.Equal(0, new AppSettings(null, 0, null).MainSplit);
    }

    /// <summary>AC 1, 11: no Ui test names the user's local application data folder; every store here gets a temp
    /// folder. (The preset store's scan already refuses the store's default-folder property in every test.)</summary>
    [Fact]
    public void NoTestNamesTheUsersLocalFolder()
    {
        // Built in parts so this file's own pattern is not a match.
        var pattern = new Regex("Local" + "ApplicationData|LOCAL" + "APPDATA", RegexOptions.IgnoreCase);
        string root = FindTestsRoot();
        var found = new List<string>();
        int scanned = 0;
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string[] segments = file.Substring(root.Length).Split('\\', '/');
            if (segments.Contains("bin") || segments.Contains("obj"))
            {
                continue;
            }

            scanned++;
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    found.Add(Path.GetFileName(file) + ":" + (i + 1));
                }
            }
        }

        Assert.True(scanned > 20, "Only " + scanned + " files were scanned under " + root);
        Assert.Empty(found);
    }

    private static string FindTestsRoot()
    {
        string start = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NameShift.slnx")))
            {
                return Path.Combine(dir.FullName, "tests", "NameShift.Ui.Tests");
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("NameShift.slnx was not found above " + start);
    }
}
