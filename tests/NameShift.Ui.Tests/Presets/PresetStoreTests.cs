using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NameShift.Core;
using NameShift.Ui.Presets;
using Xunit;
using Xunit.Abstractions;

namespace NameShift.Ui.Tests.Presets;

/// <summary>NS-36 AC 8 to 13: the preset store, against real temp folders (never the user's presets folder).</summary>
public sealed class PresetStoreTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _root;
    private readonly string _folder;

    public PresetStoreTests(ITestOutputHelper output)
    {
        _output = output;
        _root = Path.Combine(Path.GetTempPath(), "NameShift.PresetTests." + Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_root, "presets");
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

    private PresetStore Store() => new PresetStore(_folder);

    private static Preset PresetNamed(string name, string find = "a") => new Preset(name, new[]
    {
        new PresetPanel(RuleStage.Replace, true, new Dictionary<string, string> { ["Find"] = find }),
    });

    private string WriteRaw(string fileName, string text, string? folder = null)
    {
        string dir = folder ?? _folder;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, new UTF8Encoding(false).GetBytes(text));
        return path;
    }

    private string WritePreset(string fileName, string presetName, string? folder = null)
    {
        string dir = folder ?? _folder;
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, PresetJson.Serialize(PresetNamed(presetName)));
        return path;
    }

    private string[] FileNames() =>
        Directory.GetFiles(_folder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()!;

    [Fact]
    public void Save_NewName_Written_ThenListed()
    {
        PresetStore store = Store();

        PresetSaveResult result = store.Save(PresetNamed("Photos"), overwrite: false);

        Assert.Equal(PresetSaveStatus.Saved, result.Status);
        Assert.Equal(Path.Combine(_folder, "Photos.json"), result.FilePath);
        Assert.True(File.Exists(result.FilePath));
        PresetEntry entry = Assert.Single(store.List().Presets);
        Assert.Equal("Photos", entry.Name);
        Assert.Equal(result.FilePath, entry.FilePath);
    }

    [Fact]
    public void Save_NameUsed_NoOverwrite_Exists()
    {
        PresetStore store = Store();
        string path = store.Save(PresetNamed("Photos"), overwrite: false).FilePath!;
        byte[] before = File.ReadAllBytes(path);

        PresetSaveResult result = store.Save(PresetNamed("photos", find: "other"), overwrite: false);

        Assert.Equal(PresetSaveStatus.Exists, result.Status);
        Assert.Null(result.FilePath);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(new[] { "Photos.json" }, FileNames());
    }

    [Fact]
    public void Save_Overwrite_ReplacesThatFile()
    {
        string path = WritePreset("Photos (2).json", "Photos");
        PresetStore store = Store();

        PresetSaveResult result = store.Save(PresetNamed("Photos", find: "new"), overwrite: true);

        Assert.Equal(PresetSaveStatus.Saved, result.Status);
        Assert.Equal(path, result.FilePath);
        Assert.Equal(new[] { "Photos (2).json" }, FileNames());
        Assert.Equal("new", store.Load("Photos").Preset!.Panels[0].Settings["Find"]);
    }

    [Fact]
    public void Save_NameDecidedFromInside()
    {
        string path = WritePreset("Other.json", "Photos");
        byte[] before = File.ReadAllBytes(path);

        PresetSaveResult result = Store().Save(PresetNamed("Photos"), overwrite: false);

        Assert.Equal(PresetSaveStatus.Exists, result.Status);
        Assert.Equal(new[] { "Other.json" }, FileNames());
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Save_CorruptFileNeverReplaced()
    {
        string corrupt = WriteRaw("Photos.json", "{");
        PresetStore store = Store();

        PresetSaveResult result = store.Save(PresetNamed("Photos"), overwrite: true);

        Assert.Equal(PresetSaveStatus.Saved, result.Status);
        Assert.Equal(Path.Combine(_folder, "Photos (2).json"), result.FilePath);
        Assert.Equal("{", File.ReadAllText(corrupt));
        PresetListing listing = store.List();
        Assert.Equal("Photos", Assert.Single(listing.Presets).Name);
        Assert.Equal(1, listing.UnusableCount);
    }

    [Fact]
    public void Save_WriteFails_OldIntact_NoTemp_Message()
    {
        string path = Store().Save(PresetNamed("Photos"), overwrite: false).FilePath!;
        byte[] before = File.ReadAllBytes(path);
        var files = new DiskPresetFiles { BeforeReplace = _ => throw new IOException("Disk full.") };
        var store = new PresetStore(_folder, files);

        PresetSaveResult result = store.Save(PresetNamed("Photos", find: "new"), overwrite: true);

        Assert.Equal(PresetSaveStatus.Failed, result.Status);
        Assert.Equal("Disk full.", result.SystemMessage);
        Assert.Null(result.FilePath);
        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
        Assert.Equal(new[] { "Photos.json" }, FileNames());
    }

    [Fact]
    public void Save_WriteFails_NewFile_NoTemp()
    {
        var files = new DiskPresetFiles { BeforeReplace = _ => throw new IOException("Disk full.") };

        PresetSaveResult result = new PresetStore(_folder, files).Save(PresetNamed("Photos"), overwrite: false);

        Assert.Equal(PresetSaveStatus.Failed, result.Status);
        Assert.Empty(Directory.GetFiles(_folder));
    }

    [Fact]
    public void Save_FolderNamedLikeTheFile_TakesTheNextName()
    {
        string blocking = Path.Combine(_folder, "Photos.json");
        Directory.CreateDirectory(blocking);

        PresetSaveResult result = Store().Save(PresetNamed("Photos"), overwrite: false);

        Assert.Equal(PresetSaveStatus.Saved, result.Status);
        Assert.Equal(Path.Combine(_folder, "Photos (2).json"), result.FilePath);
        Assert.True(Directory.Exists(blocking));
    }

    [Fact]
    public void Save_LeftoverTemp_Tolerated()
    {
        string temp = WriteRaw("Photos.0123456789abcdef0123456789abcdef.tmp", "{ half");
        PresetStore store = Store();

        Assert.Empty(store.List().Presets);
        Assert.Equal(0, store.List().UnusableCount);
        PresetSaveResult result = store.Save(PresetNamed("Photos"), overwrite: false);

        Assert.Equal(PresetSaveStatus.Saved, result.Status);
        Assert.Equal(Path.Combine(_folder, "Photos.json"), result.FilePath);
        Assert.Equal("{ half", File.ReadAllText(temp));
        Assert.Single(store.List().Presets);
    }

    [Fact]
    public void Save_CreatesFolderOnFirstSave()
    {
        PresetStore store = Store();
        store.List();
        store.Load("Photos");
        store.Delete("Photos");
        Assert.False(Directory.Exists(_folder));

        Assert.Equal(PresetSaveStatus.Saved, store.Save(PresetNamed("Photos"), overwrite: false).Status);

        Assert.True(Directory.Exists(_folder));
    }

    [Fact]
    public void Save_HostileName_SafeFile_TypedNameListed()
    {
        PresetStore store = Store();

        PresetSaveResult photos = store.Save(PresetNamed("Photos: 2024/raw?"), overwrite: false);
        PresetSaveResult con = store.Save(PresetNamed("CON"), overwrite: false);
        PresetSaveResult traversal = store.Save(PresetNamed(@"..\victim"), overwrite: false);

        Assert.Equal(Path.Combine(_folder, "Photos_ 2024_raw_.json"), photos.FilePath);
        Assert.Equal(Path.Combine(_folder, "CON_.json"), con.FilePath);
        Assert.Equal(Path.Combine(_folder, ".._victim.json"), traversal.FilePath);
        Assert.Equal(
            new[] { @"..\victim", "CON", "Photos: 2024/raw?" }.OrderBy(n => n, NaturalNameComparer.Instance),
            store.List().Presets.Select(p => p.Name));
        Assert.All(store.List().Presets, p => Assert.Equal(_folder, Path.GetDirectoryName(p.FilePath)));
        Assert.Equal(new[] { "presets" }, Directory.GetFileSystemEntries(_root).Select(Path.GetFileName).ToArray());
    }

    [Fact]
    public void Save_SameSafeName_TwoFiles()
    {
        PresetStore store = Store();

        PresetSaveResult first = store.Save(PresetNamed("a:b", find: "1"), overwrite: false);
        PresetSaveResult second = store.Save(PresetNamed("a_b", find: "2"), overwrite: false);

        Assert.Equal(Path.Combine(_folder, "a_b.json"), first.FilePath);
        Assert.Equal(Path.Combine(_folder, "a_b (2).json"), second.FilePath);
        Assert.Equal("1", store.Load("a:b").Preset!.Panels[0].Settings["Find"]);
        Assert.Equal("2", store.Load("a_b").Preset!.Panels[0].Settings["Find"]);
        Assert.Equal(2, store.List().Presets.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Save_BlankName_Invalid(string name)
    {
        PresetSaveResult result = Store().Save(PresetNamed(name), overwrite: true);

        Assert.Equal(PresetSaveStatus.InvalidName, result.Status);
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void List_SortedNatural_IgnoringCase()
    {
        PresetStore store = Store();
        foreach (string name in new[] { "file10", "File2", "file1" })
        {
            store.Save(PresetNamed(name), overwrite: false);
        }

        Assert.Equal(new[] { "file1", "File2", "file10" }, store.List().Presets.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void List_UnusableCounted_NeverChanged()
    {
        string corrupt = WriteRaw("corrupt.json", "{");
        string newer = WriteRaw("newer.json", "{\"version\":2,\"name\":\"Future\",\"panels\":[]}");
        DateTime corruptTime = File.GetLastWriteTimeUtc(corrupt);
        DateTime newerTime = File.GetLastWriteTimeUtc(newer);

        PresetListing listing = Store().List();

        Assert.Empty(listing.Presets);
        Assert.Equal(2, listing.UnusableCount);
        Assert.Equal("{", File.ReadAllText(corrupt));
        Assert.Equal("{\"version\":2,\"name\":\"Future\",\"panels\":[]}", File.ReadAllText(newer));
        Assert.Equal(corruptTime, File.GetLastWriteTimeUtc(corrupt));
        Assert.Equal(newerTime, File.GetLastWriteTimeUtc(newer));
    }

    [Fact]
    public void List_MissingFolder_Empty_NotCreated()
    {
        PresetListing listing = Store().List();

        Assert.Empty(listing.Presets);
        Assert.Equal(0, listing.UnusableCount);
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void List_TwoFilesOneName_BothListed_FirstByPathIsThePreset()
    {
        WritePreset("a.json", "Photos");
        string second = WritePreset("b.json", "photos");
        PresetStore store = Store();

        Assert.Equal(2, store.List().Presets.Count);
        Assert.Equal(PresetDeleteStatus.Deleted, store.Delete("PHOTOS").Status);
        Assert.Equal(new[] { "b.json" }, FileNames());
        Assert.Equal(second, Assert.Single(store.List().Presets).FilePath);
    }

    [Fact]
    public void Load_Found_And_NotFound()
    {
        PresetStore store = Store();
        store.Save(PresetNamed("Photos", find: "x"), overwrite: false);

        PresetLoadResult found = store.Load("PHOTOS");

        Assert.True(found.Found);
        Assert.Equal("Photos", found.Preset!.Name);
        Assert.Equal("x", found.Preset.Panels[0].Settings["Find"]);
        Assert.False(store.Load("Music").Found);
        Assert.Null(store.Load("Music").Preset);
        Assert.False(store.Load("").Found);
        Assert.False(store.Load(null!).Found);
    }

    [Fact]
    public void Delete_RemovesOnlyThatFile()
    {
        PresetStore store = Store();
        string photos = store.Save(PresetNamed("Photos"), overwrite: false).FilePath!;
        string music = store.Save(PresetNamed("Music"), overwrite: false).FilePath!;
        string corrupt = WriteRaw("corrupt.json", "{");

        PresetDeleteResult result = store.Delete("photos");

        Assert.Equal(PresetDeleteStatus.Deleted, result.Status);
        Assert.Null(result.SystemMessage);
        Assert.False(File.Exists(photos));
        Assert.True(File.Exists(music));
        Assert.Equal("{", File.ReadAllText(corrupt));
        Assert.Equal(PresetDeleteStatus.NotFound, store.Delete("Photos").Status);
        Assert.Equal(PresetDeleteStatus.NotFound, store.Delete("  ").Status);
        Assert.True(Directory.Exists(_folder));
    }

    [Theory]
    [InlineData(@"..\victim")]
    [InlineData("{root}\\victim")]
    [InlineData(@"..\sibling\victim")]
    [InlineData("victim")]
    public void Delete_Traversal_NotFound_VictimUntouched(string name)
    {
        string above = WritePreset("victim.json", "victim", _root);
        string beside = WritePreset("victim.json", "victim", Path.Combine(_root, "sibling"));
        byte[] aboveBytes = File.ReadAllBytes(above);
        byte[] besideBytes = File.ReadAllBytes(beside);
        PresetStore store = Store();
        store.Save(PresetNamed("keep"), overwrite: false);

        PresetDeleteResult result = store.Delete(name.Replace("{root}", _root));

        Assert.Equal(PresetDeleteStatus.NotFound, result.Status);
        Assert.Equal(aboveBytes, File.ReadAllBytes(above));
        Assert.Equal(besideBytes, File.ReadAllBytes(beside));
        Assert.Single(store.List().Presets);
    }

    [Fact]
    public void Delete_CorruptFileName_NotFound()
    {
        string corrupt = WriteRaw("Photos.json", "{");

        PresetDeleteResult result = Store().Delete("Photos");

        Assert.Equal(PresetDeleteStatus.NotFound, result.Status);
        Assert.Equal("{", File.ReadAllText(corrupt));
    }

    // In use: held open with FileShare.Read, so the store can still read and list it but Windows refuses the delete.
    // (FileShare.None would make the scan skip it as unreadable, and the result would be NotFound, not Failed.)
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Delete_ReadOnlyOrInUse_Failed_StoreUnchanged(bool readOnly)
    {
        PresetStore store = Store();
        string path = store.Save(PresetNamed("Photos"), overwrite: false).FilePath!;
        byte[] before = File.ReadAllBytes(path);
        FileStream? held = null;
        PresetDeleteResult result;
        try
        {
            if (readOnly)
            {
                File.SetAttributes(path, FileAttributes.ReadOnly);
            }
            else
            {
                held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            result = store.Delete("Photos");

            Assert.Equal("Photos", Assert.Single(store.List().Presets).Name);
        }
        finally
        {
            held?.Dispose();
            File.SetAttributes(path, FileAttributes.Normal);
        }

        Assert.Equal(PresetDeleteStatus.Failed, result.Status);
        Assert.False(string.IsNullOrEmpty(result.SystemMessage));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void Delete_ListedPathOutsideFolderOrNotJson_NotFound_NothingDeleted()
    {
        // Defence in depth: a listing (here a fake) that ever returned such a path must not make Delete act on it.
        var files = new FakePresetFiles(_folder);
        string outside = Path.Combine(_folder, "..", "outside", "victim.json");
        string notJson = Path.Combine(_folder, "other.txt");
        string elsewhere = Path.Combine(_root, "victim2.json");
        files.Add(outside, PresetJson.Serialize(PresetNamed("victim")));
        files.Add(notJson, PresetJson.Serialize(PresetNamed("other")));
        files.Add(elsewhere, PresetJson.Serialize(PresetNamed("victim2")));
        var store = new PresetStore(_folder, files);

        Assert.Equal(PresetDeleteStatus.NotFound, store.Delete("victim").Status);
        Assert.Equal(PresetDeleteStatus.NotFound, store.Delete("other").Status);
        Assert.Equal(PresetDeleteStatus.NotFound, store.Delete("victim2").Status);
        Assert.Empty(files.Deleted);
    }

    [Fact]
    public void Scan_FileOverOneMiB_NotRead_Unusable()
    {
        var files = new FakePresetFiles(_folder);
        string large = Path.Combine(_folder, "large.json");
        string limit = Path.Combine(_folder, "limit.json");
        files.Add(large, PresetJson.Serialize(PresetNamed("Large")), length: 300L * 1024 * 1024);
        files.Add(limit, PresetJson.Serialize(PresetNamed("Limit")), length: PresetJson.MaxContentLength);
        var store = new PresetStore(_folder, files);

        PresetListing listing = store.List();

        Assert.Equal(1, listing.UnusableCount);
        Assert.Equal("Limit", Assert.Single(listing.Presets).Name);
        Assert.DoesNotContain(large, files.Read);
        Assert.Contains(limit, files.Read);
    }

    [Fact]
    public void DiskListing_KeepsOnlyJsonNames()
    {
        string[] names = { "a.json", "b.JSON", "c.jsonx", "d.json.0123.tmp", "e.txt", "json" };
        string[] paths = names.Select(n => Path.Combine(_folder, n)).ToArray();

        Assert.Equal(new[] { paths[0], paths[1] }, DiskPresetFiles.OnlyJson(paths));
    }

    [Fact]
    public void List_200Presets_UnderOneSecond()
    {
        PresetStore store = Store();
        for (int i = 0; i < 200; i++)
        {
            Assert.Equal(PresetSaveStatus.Saved, store.Save(PresetNamed("Preset " + i), overwrite: false).Status);
        }

        var watch = Stopwatch.StartNew();
        PresetListing listing = store.List();
        watch.Stop();

        Assert.Equal(200, listing.Presets.Count);
        _output.WriteLine("List() of 200 presets: " + watch.ElapsedMilliseconds + " ms");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), watch.ElapsedMilliseconds + " ms");
    }

    [Fact]
    public void Ctor_BlankFolder_Throws_AndFolderIsFull()
    {
        Assert.Throws<ArgumentException>(() => new PresetStore(""));
        Assert.Throws<ArgumentException>(() => new PresetStore("  "));
        Assert.Throws<ArgumentException>(() => new PresetStore(null!));
        Assert.Equal(_folder, new PresetStore(_folder + Path.DirectorySeparatorChar).Folder);
    }

    // NS-91 AC 6: the presets folder's pure part, with a fake temp path (nothing is written).

    [Theory]
    [InlineData(@"C:\Users\u\AppData\Roaming")]
    [InlineData(@"C:\Users\u\AppData\Roaming\")]
    public void FolderFor_Normal_IsTodaysPath(string profileFolder)
    {
        Assert.Equal(@"C:\Users\u\AppData\Roaming\NameShift\presets", PresetStore.FolderFor(profileFolder, _root));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData(@"\foo")]
    public void FolderFor_NoProfileFolder_IsTempFolder(string? profileFolder)
    {
        Assert.Equal(Path.Combine(_root, "NameShift", "presets"), PresetStore.FolderFor(profileFolder, _root));
    }

    [Fact]
    public void DefaultFolder_IsRoamingAppData()
    {
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NameShift", "presets");

        Assert.Equal(expected, PresetStore.DefaultFolder);
    }

    [Fact]
    public void NoTestUsesTheDefaultFolder()
    {
        // Built in two parts so this file's own pattern is not a match.
        var pattern = new Regex("\\b" + "Default" + "Folder\\b");
        string root = FindRoot();
        var found = new List<string>();
        int scanned = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories))
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
                foreach (Match match in pattern.Matches(lines[i]))
                {
                    found.Add(Path.GetFileName(file) + ":" + (i + 1));
                }
            }
        }

        Assert.True(scanned > 20, "Only " + scanned + " files were scanned under " + root);
        string only = Assert.Single(found);
        Assert.StartsWith(nameof(PresetStoreTests) + ".cs:", only);
    }

    /// <summary>An in-memory IPresetFiles: lists every file added, whatever its folder or name, and records reads and
    /// deletes.</summary>
    private sealed class FakePresetFiles : IPresetFiles
    {
        private readonly string _folder;
        private readonly Dictionary<string, (byte[] Content, long Length)> _files =
            new Dictionary<string, (byte[] Content, long Length)>(StringComparer.OrdinalIgnoreCase);

        public FakePresetFiles(string folder)
        {
            _folder = folder;
        }

        public List<string> Read { get; } = new List<string>();

        public List<string> Deleted { get; } = new List<string>();

        public void Add(string path, byte[] content, long? length = null) => _files[path] = (content, length ?? content.Length);

        public bool FolderExists(string folder) => string.Equals(folder, _folder, StringComparison.OrdinalIgnoreCase);

        public IReadOnlyList<string> ListJsonFiles(string folder) => _files.Keys.ToList();

        public bool EntryExists(string path) => _files.ContainsKey(path);

        public long FileLength(string path) => _files[path].Length;

        public byte[] ReadAll(string path)
        {
            Read.Add(path);
            return _files[path].Content;
        }

        public void WriteAtomic(string path, byte[] content) => _files[path] = (content, content.Length);

        public void Delete(string path)
        {
            Deleted.Add(path);
            _files.Remove(path);
        }
    }

    [Fact]
    public void Save_TrailingSpace_SameAsTrimmed()
    {
        PresetStore store = Store();
        Assert.Equal(PresetSaveStatus.Saved, store.Save(PresetNamed("Photos"), overwrite: false).Status);

        Assert.Equal(PresetSaveStatus.Exists, store.Save(PresetNamed("Photos "), overwrite: false).Status);
        Assert.Equal(PresetSaveStatus.Saved, store.Save(PresetNamed(" PHOTOS", "b"), overwrite: true).Status);

        Assert.Single(Directory.GetFiles(_folder, "*.json"));
        Assert.Equal("PHOTOS", Assert.Single(store.List().Presets).Name);
    }

    [Fact]
    public void Save_WritesTrimmedNameInside()
    {
        PresetStore store = Store();

        PresetSaveResult result = store.Save(PresetNamed(" Photos "), overwrite: false);

        Assert.Equal(PresetSaveStatus.Saved, result.Status);
        PresetRead read = PresetJson.Read(File.ReadAllBytes(result.FilePath!));
        Assert.Equal("Photos", read.Preset!.Name);
        Assert.Equal("Photos", Assert.Single(store.List().Presets).Name);
    }

    [Fact]
    public void HandWrittenNameWithSpaces_FoundTrimmed()
    {
        string path = WritePreset("hand.json", "Photos ");
        PresetStore store = Store();

        PresetLoadResult exact = store.Load("Photos");
        Assert.True(exact.Found);
        Assert.Equal("Photos", exact.Preset!.Name);
        Assert.True(store.Load(" photos ").Found);
        Assert.Equal("Photos", Assert.Single(store.List().Presets).Name);

        Assert.Equal(PresetDeleteStatus.Deleted, store.Delete("Photos").Status);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void InnerSpacesKept()
    {
        PresetStore store = Store();

        Assert.Equal(PresetSaveStatus.Saved, store.Save(PresetNamed("My  photos"), overwrite: false).Status);
        Assert.Equal(PresetSaveStatus.Saved, store.Save(PresetNamed("My photos"), overwrite: false).Status);

        Assert.Equal(2, store.List().Presets.Count);
    }

    private static string FindRoot()
    {
        string start = AppContext.BaseDirectory;
        DirectoryInfo? dir = new DirectoryInfo(start);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NameShift.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("NameShift.slnx was not found above " + start);
    }
}
