using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Presets;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Files;
using NameShift.Ui.Tests.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Presets;

/// <summary>NS-72 AC 3, 4, 5 and 7 to 12: the Presets menu's state and flows, over real temp folders (never the
/// user's presets folder).</summary>
public sealed class PresetMenuModelTests : IDisposable
{
    private readonly string _root;
    private readonly string _folder;
    private readonly GatedPresetFiles _files = new GatedPresetFiles();
    private readonly FakeRulePanelModel _replace = FakeRulePanelModel.Replace();
    private readonly FakeRulePanelModel _add = FakeRulePanelModel.Add();
    private bool _busy;

    public PresetMenuModelTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "NameShift.Ui.Tests", Guid.NewGuid().ToString("N"));
        _folder = Path.Combine(_root, "presets");
        Directory.CreateDirectory(_root);
        Store = new PresetStore(_folder, _files);
        Host = new RulePanelHostModel(new IRulePanelModel[] { _replace, _add });
        Model = new PresetMenuModel(Store, Host, () => _busy);
    }

    private PresetStore Store { get; }

    private RulePanelHostModel Host { get; }

    private PresetMenuModel Model { get; }

    public void Dispose()
    {
        _files.Gate?.Dispose();
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

    private static Preset PresetNamed(string name, string text = "a") => new Preset(name, new[]
    {
        new PresetPanel(RuleStage.Replace, true, new Dictionary<string, string> { ["Text"] = text }),
    });

    private void Seed(params string[] names)
    {
        foreach (string name in names)
        {
            Assert.Equal(PresetSaveStatus.Saved, Store.Save(PresetNamed(name), overwrite: false).Status);
        }
    }

    private string FileOf(string name) => Store.List().Presets.Single(e => e.Name == name).FilePath;

    private string[] ItemTexts() => Model.Items.Select(i => i.Text).ToArray();

    private static Func<string, bool> Answer(bool answer, List<string>? asked = null) => question =>
    {
        asked?.Add(question);
        return answer;
    };

    [Fact]
    public async Task Items_OnePerPreset_InStoreOrder()
    {
        Seed("file10", "File2", "file1");

        await TestWait.Within(Model.OpenAsync());

        Assert.Equal(new[] { "file1", "File2", "file10" }, ItemTexts());
        Assert.Equal(new[] { "file1", "File2", "file10" }, Model.Items.Select(i => i.PresetName).ToArray());
        Assert.All(Model.Items, item => Assert.True(item.Enabled));
    }

    [Fact]
    public async Task Items_NoPreset_OneDisabledItem()
    {
        await TestWait.Within(Model.OpenAsync());

        PresetMenuItem item = Assert.Single(Model.Items);
        Assert.Equal("No saved presets", item.Text);
        Assert.False(item.Enabled);
        Assert.Null(item.PresetName);
    }

    [Fact]
    public void CanSave_FollowsCanResetAny()
    {
        Assert.False(Model.CanSave);

        _replace.Enabled = true;
        Assert.True(Model.CanSave);

        _replace.Enabled = false;
        Assert.False(Model.CanSave);

        _add.Text = "changed";
        Assert.False(_add.Enabled);
        Assert.True(Model.CanSave);
    }

    [Fact]
    public async Task Items_AmpersandDoubled()
    {
        Seed("Fish & Chips");

        await TestWait.Within(Model.OpenAsync());

        PresetMenuItem item = Assert.Single(Model.Items);
        Assert.Equal("Fish && Chips", item.Text);
        Assert.Equal("Fish & Chips", item.PresetName);
    }

    [Fact]
    public async Task Open_ShowsLoadingUntilTheStoreAnswers()
    {
        Seed("One");
        int changed = 0;
        Model.ItemsChanged += (_, _) => changed++;
        using var gate = new TestGate();
        _files.Gate = gate;

        Task<string?> opening = Model.OpenAsync();
        try
        {
            gate.WaitUntilReached();
            PresetMenuItem loading = Assert.Single(Model.Items);
            Assert.Equal(Strings.Loading, loading.Text);
            Assert.False(loading.Enabled);
            Assert.Equal(1, changed);
        }
        finally
        {
            gate.Release();
        }

        await TestWait.Within(opening);
        Assert.Equal(new[] { "One" }, ItemTexts());
        Assert.Equal(2, changed);
    }

    [Fact]
    public void Open_NothingReadAtConstruction()
    {
        Assert.Equal(0, _files.ListCalls);
        PresetMenuItem item = Assert.Single(Model.Items);
        Assert.Equal(Strings.Loading, item.Text);
    }

    [Fact]
    public async Task Open_Superseded_OnlyTheLatestApplies()
    {
        Seed("One");
        using var gate = new TestGate();
        _files.Gate = gate;
        Task<string?> first = Model.OpenAsync();
        try
        {
            gate.WaitUntilReached();
            _files.Gate = null;
            Seed("Two");

            string? second = await TestWait.Within(Model.OpenAsync());
            Assert.Null(second);
            Assert.Equal(new[] { "One", "Two" }, ItemTexts());
        }
        finally
        {
            gate.Release();
        }

        Assert.Null(await TestWait.Within(first));
        Assert.Equal(new[] { "One", "Two" }, ItemTexts());
    }

    [Fact]
    public async Task Open_UnreadableFiles_Message()
    {
        Seed("Good");
        File.WriteAllBytes(Path.Combine(_folder, "broken.json"), new UTF8Encoding(false).GetBytes("not json"));

        Assert.Equal("1 preset could not be read.", await TestWait.Within(Model.OpenAsync()));
        Assert.Equal(new[] { "Good" }, ItemTexts());
        Assert.Equal("1 preset could not be read.", await TestWait.Within(Model.OpenAsync()));
    }

    [Fact]
    public async Task Open_MissingFolder_NoMessage()
    {
        Assert.Null(await TestWait.Within(Model.OpenAsync()));
        Assert.Equal(new[] { "No saved presets" }, ItemTexts());
    }

    [Fact]
    public async Task Save_NewName_Saved()
    {
        _replace.Text = "a";
        _replace.Enabled = true;
        var asked = new List<string>();

        string? status = await TestWait.Within(Model.SaveAsync(" X ", Answer(true, asked)));

        Assert.Equal("Preset \"X\" saved.", status);
        Assert.Equal("X", Assert.Single(Store.List().Presets).Name);
        Assert.Equal("X", Model.LastName);
        Assert.Empty(asked);
    }

    [Fact]
    public async Task Save_Exists_No_WritesNothing()
    {
        Seed("X");
        byte[] before = File.ReadAllBytes(FileOf("X"));
        _replace.Text = "new";
        _replace.Enabled = true;
        var asked = new List<string>();

        string? status = await TestWait.Within(Model.SaveAsync("X", Answer(false, asked)));

        Assert.Null(status);
        Assert.Equal(new[] { "Replace the preset \"X\"?" }, asked);
        Assert.Equal(before, File.ReadAllBytes(FileOf("X")));
        Assert.Equal("", Model.LastName);
    }

    [Fact]
    public async Task Save_Exists_Yes_Replaces()
    {
        Seed("X");
        _replace.Text = "new";
        _replace.Enabled = true;

        string? status = await TestWait.Within(Model.SaveAsync("X", Answer(true)));

        Assert.Equal("Preset \"X\" saved.", status);
        PresetEntry entry = Assert.Single(Store.List().Presets);
        PresetRead read = PresetJson.Read(File.ReadAllBytes(entry.FilePath));
        Assert.Equal("new", read.Preset!.Panels.Single().Settings["Text"]);
        Assert.Single(Directory.GetFiles(_folder, "*.json"));
    }

    [Fact]
    public async Task Save_ExistsIgnoringCaseAndSpaces_Asks()
    {
        Seed("Photos");
        _replace.Enabled = true;
        var asked = new List<string>();

        await TestWait.Within(Model.SaveAsync("photos ", Answer(false, asked)));

        Assert.Equal(new[] { "Replace the preset \"photos\"?" }, asked);
    }

    [Fact]
    public async Task Save_StoreFails_Message()
    {
        File.WriteAllText(_folder, "this is a file, not a folder");
        _replace.Enabled = true;

        string? status = await TestWait.Within(Model.SaveAsync("X", Answer(true)));

        Assert.StartsWith("The preset could not be saved: ", status);
        Assert.True(status!.Length > "The preset could not be saved: ".Length);
        Assert.Equal("", Model.LastName);
        Assert.True(File.Exists(_folder));
    }

    [Fact]
    public async Task Load_AppliesPreset_OneRulesChanged()
    {
        _replace.Text = "a";
        _replace.Enabled = true;
        Seed("X");
        _replace.Text = "zzz";
        _add.Enabled = true;
        int rulesChanged = 0;
        Host.RulesChanged += (_, _) => rulesChanged++;

        string? status = await TestWait.Within(Model.LoadAsync("X", () => false));

        Assert.Equal("Preset \"X\" loaded.", status);
        Assert.Equal("a", _replace.Text);
        Assert.True(_replace.Enabled);
        Assert.False(_add.Enabled);
        Assert.Equal(1, rulesChanged);
        Assert.Equal("X", Model.LastName);
    }

    [Fact]
    public async Task Load_ClosedMeanwhile_NothingApplied()
    {
        Seed("X");
        _replace.Text = "keep";
        int rulesChanged = 0;
        Host.RulesChanged += (_, _) => rulesChanged++;

        string? status = await TestWait.Within(Model.LoadAsync("X", () => true));

        Assert.Null(status);
        Assert.Equal("keep", _replace.Text);
        Assert.Equal(0, rulesChanged);
        Assert.Equal("", Model.LastName);
    }

    [Fact]
    public async Task Load_BusyMeanwhile_NotApplied()
    {
        Seed("X");
        _replace.Text = "keep";
        _busy = true;

        string? status = await TestWait.Within(Model.LoadAsync("X", () => false));

        Assert.Equal("The preset was not loaded: a rename is running.", status);
        Assert.Equal("keep", _replace.Text);
    }

    [Fact]
    public async Task Load_Vanished_NotFound()
    {
        Seed("X");
        await TestWait.Within(Model.OpenAsync());
        File.Delete(FileOf("X"));
        _replace.Text = "keep";

        string? status = await TestWait.Within(Model.LoadAsync("X", () => false));

        Assert.Equal("The preset \"X\" was not found.", status);
        Assert.Equal("keep", _replace.Text);
    }

    [Fact]
    public async Task Delete_Yes_Deleted()
    {
        Seed("X");
        string path = FileOf("X");
        var asked = new List<string>();

        string? status = await TestWait.Within(Model.DeleteAsync("X", Answer(true, asked)));

        Assert.Equal("Preset \"X\" deleted.", status);
        Assert.Equal(new[] { "Delete the preset \"X\"?" }, asked);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Delete_No_NothingHappens()
    {
        Seed("X");
        string path = FileOf("X");

        string? status = await TestWait.Within(Model.DeleteAsync("X", Answer(false)));

        Assert.Null(status);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task Delete_Vanished_NotFound()
    {
        string? status = await TestWait.Within(Model.DeleteAsync("X", Answer(true)));

        Assert.Equal("The preset \"X\" was not found.", status);
    }

    [Fact]
    public async Task Delete_Fails_Message()
    {
        Seed("X");
        string path = FileOf("X");
        File.SetAttributes(path, FileAttributes.ReadOnly);

        string? status = await TestWait.Within(Model.DeleteAsync("X", Answer(true)));

        Assert.StartsWith("The preset could not be deleted: ", status);
        Assert.True(status!.Length > "The preset could not be deleted: ".Length);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void Ctor_Nulls_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new PresetMenuModel(null!, Host, () => false));
        Assert.Throws<ArgumentNullException>(() => new PresetMenuModel(Store, null!, () => false));
        Assert.Throws<ArgumentNullException>(() => new PresetMenuModel(Store, Host, null!));
    }
}
