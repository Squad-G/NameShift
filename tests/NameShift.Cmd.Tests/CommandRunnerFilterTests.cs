using System;
using System.IO;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Presets;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The filter options and the folder mask through <see cref="CommandRunner"/>: over a fake listing for what is
/// kept, over real temp folders (and a temp history root) for what is renamed.</summary>
public sealed class CommandRunnerFilterTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private RenameItem File(string name, ItemAttributes attributes = ItemAttributes.None) =>
        new RenameItem(name, _temp.Files, ItemKind.File, attributes);

    private RenameItem Folder(string name) => new RenameItem(name, _temp.Files, ItemKind.Folder);

    /// <summary>A dry run with the prefix x over a fake listing: one "old -> new" line per kept item.</summary>
    private (int Code, string[] Lines, string Error, FakeBatchRenamer Renamer, FakeFileListSource Source) Plan(
        RenameItem[] items, params string[] options)
    {
        var renamer = new FakeBatchRenamer();
        var source = new FakeFileListSource(FileListing.Ok(items));
        var args = new[] { "apply", _temp.Files, "--prefix", "x", "--dry-run" }.Concat(options).ToArray();
        var run = Cmd.Run(renamer, _temp.Files, args, source);
        Assert.True(run.Code == 0 || options.Contains("(") , run.Error + run.Output);
        return (run.Code, Cmd.Lines(run.Output), run.Error, renamer, source);
    }

    private static string[] Lines(params string[] names) => names.Select(n => n + " -> x" + n).ToArray();

    [Fact]
    public void Mask_KeepsMatchingNames()
    {
        var run = Plan(new[] { File("a.jpg"), File("b.PNG"), File("c.txt") }, "--mask", "*.jpg;*.png");

        Assert.Equal(0, run.Code);
        Assert.Equal(Lines("a.jpg", "b.PNG"), run.Lines);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void Mask_OnDisk_OtherFileUntouched()
    {
        _temp.File("a.jpg");
        _temp.File("b.PNG");
        _temp.File("c.txt");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--mask", "*.jpg;*.png", "--prefix", "x");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "c.txt", "xa.jpg", "xb.PNG" }, _temp.FileNames());
    }

    [Fact]
    public void FilterRegex_BaseNameOnly()
    {
        var run = Plan(new[] { File("IMG_1.jpg"), File("IMG_1x.jpg"), File("img_1.jpg") },
            "--filter-regex", "^IMG_[0-9]+$");

        Assert.Equal(Lines("IMG_1.jpg"), run.Lines);
    }

    [Fact]
    public void FilterRegex_IgnoreCase()
    {
        var run = Plan(new[] { File("IMG_1.jpg"), File("IMG_1x.jpg"), File("img_2.jpg") },
            "--filter-regex", "^IMG_[0-9]+$", "--filter-ignore-case");

        Assert.Equal(Lines("IMG_1.jpg", "img_2.jpg"), run.Lines);
    }

    [Fact]
    public void BadRegex_NothingListed()
    {
        var run = Plan(new[] { File("a.jpg") }, "--filter-regex", "(");

        string expected = CommandLineParser.Parse(new[] { "apply", _temp.Files, "--prefix", "x", "--filter-regex", "(" }).Error!;
        Assert.Equal(1, run.Code);
        Assert.StartsWith("--filter-regex: ", expected, StringComparison.Ordinal);
        Assert.Equal(Cmd.Written(expected), run.Error);
        Assert.Empty(run.Lines);
        Assert.Empty(run.Source.IncludeSubfolders);
    }

    [Fact]
    public void Lengths_BaseNameOnly()
    {
        var run = Plan(
            new[] { File("ab.txt"), File("abc.jpeg"), File("abcde.x"), File("abcdef.txt") },
            "--min-length", "3", "--max-length", "5");

        Assert.Equal(Lines("abc.jpeg", "abcde.x"), run.Lines);
    }

    [Fact]
    public void Hidden_LeftOut_IncludeHiddenBringsThem()
    {
        RenameItem[] items = { File("h.txt", ItemAttributes.Hidden), File("s.txt", ItemAttributes.System), File("p.txt") };

        Assert.Equal(Lines("p.txt"), Plan(items).Lines);
        Assert.Equal(Lines("h.txt", "p.txt", "s.txt"), Plan(items, "--include-hidden").Lines);
    }

    [Fact]
    public void Hidden_OnDisk()
    {
        _temp.File("h.txt", FileAttributes.Hidden);
        _temp.File("v.txt");

        Assert.Equal(0, Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--prefix", "x").Code);
        Assert.Equal(new[] { "h.txt", "xv.txt" }, _temp.FileNames());

        using var other = new TempFolder();
        other.File("h.txt", FileAttributes.Hidden);
        other.File("v.txt");

        Assert.Equal(0, Cmd.RunOnDisk(other, "apply", other.Files, "--prefix", "x", "--include-hidden").Code);
        Assert.Equal(new[] { "xh.txt", "xv.txt" }, other.FileNames());
    }

    private static RenameItem[] FileAndFolder(string folder) =>
        new[] { new RenameItem("f.txt", folder), new RenameItem("Sub", folder, ItemKind.Folder) };

    private (string[] Lines, string[] Args) KindRun(params string[] options)
    {
        var run = Plan(FileAndFolder(_temp.Files), options);
        Assert.Equal(0, run.Code);
        return (run.Lines, options);
    }

    [Fact]
    public void Kinds_Default_FilesOnly() => Assert.Equal(Lines("f.txt"), KindRun().Lines);

    [Fact]
    public void Kinds_FilesOnly_Same() => Assert.Equal(Lines("f.txt"), KindRun("--files-only").Lines);

    [Fact]
    public void Kinds_IncludeFolders_Both() =>
        Assert.Equal(Lines("Sub", "f.txt"), KindRun("--include-folders").Lines);

    [Fact]
    public void Kinds_FoldersOnly_FoldersOnly() => Assert.Equal(Lines("Sub"), KindRun("--folders-only").Lines);

    [Fact]
    public void FoldersOnly_OnDisk_JournalAndUndo()
    {
        Directory.CreateDirectory(Path.Combine(_temp.Files, "Sub"));
        _temp.File("f.txt");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--folders-only", "--prefix", "x");

        Assert.Equal(0, run.Code);
        Assert.True(Directory.Exists(Path.Combine(_temp.Files, "xSub")));
        Assert.False(Directory.Exists(Path.Combine(_temp.Files, "Sub")));
        Assert.Equal(new[] { "f.txt" }, _temp.FileNames());

        var undo = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(0, undo.Code);
        Assert.True(Directory.Exists(Path.Combine(_temp.Files, "Sub")));
        Assert.False(Directory.Exists(Path.Combine(_temp.Files, "xSub")));
    }

    [Fact]
    public void FolderMask_OnDisk()
    {
        _temp.File("a.jpg");
        _temp.File("b.txt");

        var first = Cmd.RunOnDisk(_temp, "apply", Path.Combine(_temp.Files, "*.jpg"), "--prefix", "x");

        Assert.Equal(0, first.Code);
        Assert.Equal(new[] { "b.txt", "xa.jpg" }, _temp.FileNames());

        var second = Cmd.RunOnDisk(_temp, "apply", "*.jpg", "--prefix", "y");

        Assert.Equal(0, second.Code);
        Assert.Equal(new[] { "b.txt", "yxa.jpg" }, _temp.FileNames());
    }

    [Fact]
    public void FolderMask_InMiddle_InvalidFolder()
    {
        string folder = "D:" + (char)92 + "*" + (char)92 + "x";
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", folder, "--case", "lower" });

        Assert.Equal(1, run.Code);
        Assert.Contains(string.Format(Cmd.EnUs, Strings.InvalidFolder, folder), run.Error, StringComparison.Ordinal);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void NoMatch_LineAndZero()
    {
        var renamer = new FakeBatchRenamer();
        var source = new FakeFileListSource(FileListing.Ok(new[] { File("a.jpg") }));

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--prefix", "x", "--mask", "*.zip" }, source);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("No files match."), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.RenameCalls);

        var quiet = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--prefix", "x", "--mask", "*.zip", "-q" }, source);

        Assert.Equal(0, quiet.Code);
        Assert.Equal("", quiet.Output);
        Assert.Equal("", quiet.Error);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void NoMatch_ByPathMask()
    {
        var source = new FakeFileListSource(FileListing.Ok(new[] { File("a.jpg") }));

        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files,
            new[] { "apply", Path.Combine(_temp.Files, "*.zip"), "--prefix", "x" }, source);

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("No files match."), run.Output);
    }

    [Fact]
    public void NoFilter_EmptyFolder_NothingToRename()
    {
        var run = Cmd.Run(new FakeBatchRenamer(), _temp.Files,
            new[] { "apply", _temp.Files, "--prefix", "x" }, new FakeFileListSource(FileListing.Ok(Array.Empty<RenameItem>())));

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(Strings.NothingToRename), run.Output);
        Assert.Equal("Nothing to rename.", Strings.NothingToRename);
    }

    [Fact]
    public void Filters_WithPreset()
    {
        var renamer = new FakeBatchRenamer();
        var source = new FakeFileListSource(FileListing.Ok(new[] { File("a.jpg"), File("b.txt") }));
        var save = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "upper", "--save-preset", "P", "--dry-run" },
            source, presetsFolder: _temp.Presets);
        Assert.Equal(0, save.Code);

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--preset", "P", "--mask", "*.jpg", "--dry-run" },
            source, presetsFolder: _temp.Presets);

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "a.jpg -> A.jpg" }, Cmd.Lines(run.Output));
    }

    [Fact]
    public void SavedPreset_HoldsNoFilter()
    {
        var renamer = new FakeBatchRenamer();
        var source = new FakeFileListSource(FileListing.Ok(new[] { File("a.jpg") }));
        string[] withFilter = { "apply", _temp.Files, "--case", "upper", "--mask", "*.jpg", "--include-folders", "--save-preset", "A", "--dry-run" };
        string[] without = { "apply", _temp.Files, "--case", "upper", "--save-preset", "B", "--dry-run" };

        Assert.Equal(0, Cmd.Run(renamer, _temp.Files, withFilter, source, presetsFolder: _temp.Presets).Code);
        Assert.Equal(0, Cmd.Run(renamer, _temp.Files, without, source, presetsFolder: _temp.Presets).Code);

        var store = new PresetStore(_temp.Presets);
        var a = new RulePanelHostModel(RulePanelModels.CreateAll());
        a.ApplyPreset(store.Load("A").Preset!);
        var b = new RulePanelHostModel(RulePanelModels.CreateAll());
        b.ApplyPreset(store.Load("B").Preset!);
        Assert.Equal(a.Panels.Count, b.Panels.Count);
        for (int i = 0; i < a.Panels.Count; i++)
        {
            Assert.Equal(a.Panels[i].Enabled, b.Panels[i].Enabled);
            Assert.Equal(a.Panels[i].SaveSettings(), b.Panels[i].SaveSettings());
        }

        string text = File2Text("A");
        Assert.DoesNotContain("mask", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("*.jpg", text, StringComparison.Ordinal);
    }

    private string File2Text(string name)
    {
        string file = Directory.GetFiles(_temp.Presets).Single(f => Path.GetFileName(f).StartsWith(name, StringComparison.OrdinalIgnoreCase));
        return System.IO.File.ReadAllText(file);
    }
}
