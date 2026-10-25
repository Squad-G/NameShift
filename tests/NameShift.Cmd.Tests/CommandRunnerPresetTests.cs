using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Presets;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>--preset, --save-preset and --overwrite, over a presets folder inside the temp folder: never the user's own.</summary>
public sealed class CommandRunnerPresetTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private PresetStore Store => new PresetStore(_temp.Presets);

    private static ReplacePanelModel Replace(string find, string with, bool enabled = true, bool matchCase = false) =>
        new ReplacePanelModel { Find = find, With = with, MatchCase = matchCase, Enabled = enabled };

    private static CasePanelModel CaseOf(CaseMode mode) => new CasePanelModel { Mode = mode, Enabled = true };

    private void Save(string name, params IRulePanelModel[] models)
    {
        PresetSaveResult result = Store.Save(Preset.FromPanels(name, models), false);
        Assert.Equal(PresetSaveStatus.Saved, result.Status);
    }

    private (int Code, string Output, string Error, FakeBatchRenamer Renamer) Run(params string[] args)
    {
        var renamer = new FakeBatchRenamer();
        var run = Cmd.Run(renamer, _temp.Files, args, presetsFolder: _temp.Presets);
        return (run.Code, run.Output, run.Error, renamer);
    }

    private string[] PresetFiles() =>
        Directory.Exists(_temp.Presets) ? Directory.GetFiles(_temp.Presets) : Array.Empty<string>();

    [Fact]
    public void Preset_DryRun_ListsItsNames()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        Save("Photos", Replace("IMG", "Trip"));

        var run = Run("apply", "--preset", " photos ", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Equal(new[] { "IMG_001.JPG -> Trip_001.JPG", "IMG_002.JPG -> Trip_002.JPG" }, Cmd.Lines(run.Output));
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG" }, _temp.FileNames());
        Assert.Equal(0, run.Renamer.RenameCalls);
    }

    [Fact]
    public void Preset_RealRun_RenamesAndJournals()
    {
        _temp.File("IMG_001.JPG");
        Save("Photos", Replace("IMG", "Trip"));

        var renamed = Cmd.RunOnDisk(_temp, "apply", "--preset", "Photos", _temp.Files);

        Assert.Equal(0, renamed.Code);
        Assert.Equal("", renamed.Error);
        Assert.Equal(new[] { "Trip_001.JPG" }, _temp.FileNames());
        var undo = Cmd.RunOnDisk(_temp, "undo", "--dry-run");
        Assert.Equal(0, undo.Code);
        Assert.Equal(new[] { "Trip_001.JPG -> IMG_001.JPG" }, Cmd.Lines(undo.Output));
    }

    [Fact]
    public async Task PresetEqualsTheWindow()
    {
        foreach (string name in new[]
        {
            "IMG_001.JPG", "img_002.jpeg", "My IMG.Img", "file2.txt", "file10.txt", "Été img.png", "x.tar.gz",
            "MyHolidayPic.jpg",
        })
        {
            _temp.File(name);
        }

        _temp.File("IMG_h.JPG", FileAttributes.Hidden);
        Save("Three", Replace("img", "Pic"), new NumberingPanelModel { Enabled = true }, CaseOf(CaseMode.Lower));

        var files = new FileListModel(new DiskFileListSource());
        var host = new RulePanelHostModel(RulePanelModels.CreateAll());
        host.ApplyPreset(Store.Load("Three").Preset!);
        var preview = new PreviewModel(host, files);
        await files.LoadAsync(_temp.Files);
        await preview.Pending;
        var window = new List<string>();
        for (int row = 0; row < files.Items.Count; row++)
        {
            PreviewCell cell = preview.CellForRow(row);
            if (cell.Kind == PreviewCellKind.Changed)
            {
                window.Add(files.Items[row].Name + " -> " + cell.Text);
            }
        }

        var run = Run("apply", "--preset", "Three", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        string[] cli = Cmd.Lines(run.Output);
        Assert.True(cli.Length >= 6, string.Join("|", cli));
        Assert.Equal(window.ToArray(), cli);
    }

    [Fact]
    public void NotFound_ListsSavedNames()
    {
        _temp.File("IMG_001.JPG");
        foreach (string name in new[] { "b", "A", "c" })
        {
            Save(name, CaseOf(CaseMode.Lower));
        }

        var run = Run("apply", "--preset", "x", _temp.Files);

        Assert.Equal(1, run.Code);
        Assert.Equal("", run.Output);
        Assert.Equal(Cmd.Written("Preset \"x\" was not found. Saved presets: A, b, c."), run.Error);
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void NotFound_NoPresets()
    {
        _temp.File("IMG_001.JPG");

        var run = Run("apply", "--preset", "x", _temp.Files);

        Assert.Equal(1, run.Code);
        Assert.Equal("", run.Output);
        Assert.Equal(Cmd.Written("Preset \"x\" was not found. There are no saved presets."), run.Error);
    }

    [Fact]
    public void NotFound_UnreadableFolder()
    {
        _temp.File("IMG_001.JPG");
        string notAFolder = Path.Combine(_temp.Root, "presets-file");
        System.IO.File.WriteAllText(notAFolder, "not a folder");
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(
            output, error, _temp.Files, new FakeBatchRenamer(), new DiskFileListSource(), Cmd.EnUs, "1.2.3", new PresetStore(notAFolder));

        int code = runner.Run(new[] { "apply", "--preset", "x", _temp.Files });

        Assert.Equal(1, code);
        Assert.Equal(Cmd.Written("Preset \"x\" was not found. There are no saved presets."), error.ToString());
    }

    [Fact]
    public void NoRules()
    {
        _temp.File("IMG_001.JPG");
        Save("Off", Replace("IMG", "Trip", enabled: false));
        Save("EmptyFind", Replace("", "Trip"));

        foreach (string name in new[] { "Off", "EmptyFind" })
        {
            var run = Run("apply", "--preset", name, _temp.Files);

            Assert.Equal(1, run.Code);
            Assert.Equal("", run.Output);
            Assert.Equal(Cmd.Written("The preset \"" + name + "\" has no rules."), run.Error);
            Assert.Equal(0, run.Renamer.RenameCalls);
        }

        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void OptionReplacesThePanelWhole()
    {
        _temp.File("Xa.txt");
        _temp.File("xa2.txt");
        Save("P", Replace("a", "b", matchCase: true), CaseOf(CaseMode.Upper));

        var run = Run("apply", "--preset", "P", "--replace", "x", "y", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        string[] lines = Cmd.Lines(run.Output);
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("Xa.txt -> YA", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("xa2.txt -> YA2", lines[1], StringComparison.Ordinal);
        Assert.All(lines, line => Assert.DoesNotContain("B", line.Substring(line.IndexOf("->", StringComparison.Ordinal))));
    }

    [Fact]
    public void OptionEnablesADisabledPanel()
    {
        _temp.File("Xa.txt");
        Save("P", Replace("a", "b", enabled: false), CaseOf(CaseMode.Upper));

        var run = Run("apply", "--preset", "P", "--replace", "x", "y", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.StartsWith("Xa.txt -> YA", Assert.Single(Cmd.Lines(run.Output)), StringComparison.Ordinal);
    }

    [Fact]
    public void OptionAddsAPanel()
    {
        _temp.File("ABC.txt");
        Save("P", CaseOf(CaseMode.Lower));

        var run = Run("apply", "--preset", "P", "--prefix", "p_", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.StartsWith("ABC.txt -> p_abc", Assert.Single(Cmd.Lines(run.Output)), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("b")]
    [InlineData("?")]
    public void PresetBlocked_SameAsOptions(string with)
    {
        _temp.File("a1.txt");
        _temp.File("b1.txt");
        Save("P", Replace("a", with));
        string file = Directory.GetFiles(_temp.Presets).Single();
        byte[] bytes = System.IO.File.ReadAllBytes(file);
        DateTime written = System.IO.File.GetLastWriteTimeUtc(file);

        var viaPreset = Run("apply", "--preset", "P", _temp.Files);
        var viaOptions = Cmd.Run(new FakeBatchRenamer(), _temp.Files, new[] { "apply", "--replace", "a", with, _temp.Files });

        Assert.Equal(2, viaPreset.Code);
        Assert.Equal(2, viaOptions.Code);
        Assert.NotEqual("", viaPreset.Error);
        Assert.Equal(viaOptions.Error, viaPreset.Error);
        Assert.Equal(0, viaPreset.Renamer.RenameCalls);
        Assert.Equal(bytes, System.IO.File.ReadAllBytes(file));
        Assert.Equal(written, System.IO.File.GetLastWriteTimeUtc(file));
    }

    [Fact]
    public void SavePreset_SavesThenApplies()
    {
        _temp.File("ABC.txt");

        var run = Run("apply", "--case", "lower", "--save-preset", " Mine ", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        string[] lines = Cmd.Lines(run.Output);
        Assert.Equal(2, lines.Length);
        Assert.Equal("Preset \"Mine\" saved.", lines[0]);
        Assert.StartsWith("ABC.txt -> abc", lines[1], StringComparison.Ordinal);
        Assert.Equal(new[] { "Mine" }, Store.List().Presets.Select(entry => entry.Name).ToArray());
        Assert.Equal(new[] { "ABC.txt" }, _temp.FileNames());
        Assert.Equal(0, run.Renamer.RenameCalls);
    }

    [Fact]
    public void SavePreset_ReadBackByTheWindow()
    {
        _temp.File("ABC.txt");
        string[] args = { "apply", _temp.Files, "--replace", "a", "b", "--number", "--save-preset", "P", "--dry-run" };

        var run = Run(args);

        Assert.Equal(0, run.Code);
        var cli = new RulePanelHostModel(RulePanelModels.CreateAll());
        CommandLineParser.Parse(args).Options!.ApplyTo(cli);
        var window = new RulePanelHostModel(RulePanelModels.CreateAll());
        window.ApplyPreset(Store.Load("P").Preset!);
        Assert.Equal(cli.Panels.Count, window.Panels.Count);
        for (int i = 0; i < cli.Panels.Count; i++)
        {
            Assert.Equal(cli.Panels[i].Stage, window.Panels[i].Stage);
            Assert.Equal(cli.Panels[i].Enabled, window.Panels[i].Enabled);
            Assert.Equal(cli.Panels[i].SaveSettings(), window.Panels[i].SaveSettings());
        }

        Assert.Equal(cli.Rules.Count, window.Rules.Count);
        Assert.Equal(2, window.Rules.Count);
    }

    [Fact]
    public void SavePreset_FromPresetPlusOptions()
    {
        _temp.File("ABC.txt");
        Save("A", Replace("a", "b"), CaseOf(CaseMode.Upper));

        var run = Run("apply", "--preset", "A", "--case", "lower", "--save-preset", "B", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        var window = new RulePanelHostModel(RulePanelModels.CreateAll());
        window.ApplyPreset(Store.Load("B").Preset!);
        Assert.Equal(2, window.Rules.Count);
        Assert.Equal("a", Assert.IsType<ReplaceRule>(window.Rules[0]).Find);
        Assert.Equal(CaseMode.Lower, Assert.IsType<CaseRule>(window.Rules[1]).Mode);
    }

    [Fact]
    public void SavePreset_QuietHidesTheLine()
    {
        _temp.File("ABC.txt");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower", "--save-preset", "Q", "-q");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(new[] { "Q" }, Store.List().Presets.Select(entry => entry.Name).ToArray());
        Assert.NotEqual(new[] { "ABC.txt" }, _temp.FileNames());
    }

    [Fact]
    public void SavePreset_VerbosePrintsIt()
    {
        _temp.File("ABC.txt");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower", "--save-preset", "V", "-v");

        Assert.Equal(0, run.Code);
        Assert.Equal("Preset \"V\" saved.", Cmd.Lines(run.Output)[0]);
        Assert.True(Cmd.Lines(run.Output).Length > 1);
    }

    [Fact]
    public void SavePreset_Exists()
    {
        _temp.File("ABC.txt");
        Save("Mine", CaseOf(CaseMode.Upper));
        string file = Directory.GetFiles(_temp.Presets).Single();
        byte[] bytes = System.IO.File.ReadAllBytes(file);

        var run = Run("apply", "--case", "lower", "--save-preset", " mine ", _temp.Files);

        Assert.Equal(1, run.Code);
        Assert.Equal("", run.Output);
        Assert.Equal(Cmd.Written("A preset named \"mine\" exists. Add --overwrite to replace it."), run.Error);
        Assert.Equal(bytes, System.IO.File.ReadAllBytes(file));
        Assert.Equal(0, run.Renamer.RenameCalls);
    }

    [Fact]
    public void SavePreset_Overwrite()
    {
        _temp.File("ABC.txt");
        Save("Mine", CaseOf(CaseMode.Upper));

        var run = Run("apply", "--case", "lower", "--save-preset", " mine ", "--overwrite", "--dry-run", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.Equal("Preset \"mine\" saved.", Cmd.Lines(run.Output)[0]);
        Assert.Single(PresetFiles());
        Assert.Equal(
            CaseMode.Lower,
            Assert.IsType<CaseRule>(LoadedRules("Mine").Single()).Mode);
    }

    private IReadOnlyList<IRenameRule> LoadedRules(string name)
    {
        var host = new RulePanelHostModel(RulePanelModels.CreateAll());
        host.ApplyPreset(Store.Load(name).Preset!);
        return host.Rules;
    }

    [Fact]
    public void SavePreset_Fails_Exit3()
    {
        _temp.File("ABC.txt");
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var renamer = new FakeBatchRenamer();
        var runner = new CommandRunner(
            output, error, _temp.Files, renamer, new DiskFileListSource(), Cmd.EnUs, "1.2.3",
            new PresetStore(_temp.Presets, new FailingPresetFiles()));

        int code = runner.Run(new[] { "apply", "--case", "lower", "--save-preset", "X", _temp.Files });

        Assert.Equal(3, code);
        Assert.Equal("", output.ToString());
        Assert.Equal(Cmd.Written("The preset could not be saved: Disk full."), error.ToString());
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void SavePreset_AfterFolderCheck()
    {
        string missing = Path.Combine(_temp.Root, "missing");

        var run = Run("apply", missing, "--case", "lower", "--save-preset", "X");

        Assert.Equal(1, run.Code);
        Assert.Contains("missing", run.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_temp.Presets));
    }

    [Fact]
    public void SavePreset_NoRule_SavesNothing()
    {
        var run = Run("apply", _temp.Files, "--save-preset", "X");

        Assert.Equal(1, run.Code);
        Assert.StartsWith(Strings.NoRule, run.Error, StringComparison.Ordinal);
        Assert.False(Directory.Exists(_temp.Presets));
    }

    [Fact]
    public void ApplyNeverWritesThePresetItReads()
    {
        _temp.File("IMG_001.JPG");
        Save("P", Replace("IMG", "Trip"));
        string file = Directory.GetFiles(_temp.Presets).Single();
        byte[] bytes = System.IO.File.ReadAllBytes(file);
        DateTime written = System.IO.File.GetLastWriteTimeUtc(file);

        var run = Run("apply", "--preset", "P", "--case", "lower", _temp.Files);

        Assert.Equal(0, run.Code);
        Assert.Equal(bytes, System.IO.File.ReadAllBytes(file));
        Assert.Equal(written, System.IO.File.GetLastWriteTimeUtc(file));
        Assert.Single(PresetFiles());
    }
}
