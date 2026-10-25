using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NameShift.Core;
using NameShift.Core.Tests;
using NameShift.Ui.Files;
using NameShift.Ui.Presets;
using NameShift.Ui.Rules;
using NameShift.Ui.Tests.Files;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary><c>apply --date-source taken</c> (NS-86): the photo dates are read after the filter, only on a built Photo
/// date rule, and a file without one is counted in one line on the error stream. Over a fake listing and a fake photo
/// source, and over a real temp folder with generated JPEGs.</summary>
public sealed class CommandRunnerPhotoDateTests : IDisposable
{
    private static readonly DateTime Modified = new DateTime(2024, 3, 4, 5, 6, 7);
    private static readonly DateTime Taken = new DateTime(2023, 7, 1, 10, 0, 0);

    private readonly TempFolder _temp = new TempFolder();
    private readonly FakePhotoFile _fake = new FakePhotoFile();

    public void Dispose() => _temp.Dispose();

    private string PathOf(string name) => Path.Combine(_temp.Files, name);

    private RenameItem ListedFile(string name) =>
        new RenameItem(name, _temp.Files, ItemKind.File, ItemAttributes.None, 100, Modified, Modified);

    private RenameItem Folder(string name) => new RenameItem(name, _temp.Files, ItemKind.Folder, ItemAttributes.None, 0, Modified, Modified);

    private (int Code, string Output, string Error) Run(FileListing listing, params string[] options)
    {
        var args = new[] { "apply", _temp.Files }.Concat(options).ToArray();
        return Cmd.Run(
            new FakeBatchRenamer(), _temp.Files, args, new FakeFileListSource(listing),
            photoDates: new PhotoDates(_fake, PhotoDates.DefaultCapacity));
    }

    private (int Code, string Output, string Error) Run(RenameItem[] items, params string[] options) =>
        Run(FileListing.Ok(items), options);

    private static string[] TakenArgs(params string[] more) =>
        new[] { "--date", "--date-source", "taken", "--dry-run" }.Concat(more).ToArray();

    [Fact]
    public void Taken_UsesPhotoDate()
    {
        _fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Taken));

        var run = Run(new[] { ListedFile("a.jpg") }, TakenArgs());

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "a.jpg -> 2023-07-01_a.jpg" }, Cmd.Lines(run.Output));
        Assert.Equal("", run.Error);
        Assert.Equal(1, _fake.Calls);
    }

    [Theory]
    [InlineData(1, "1 file has no photo date; its modified date was used.")]
    [InlineData(2, "2 files have no photo date; their modified date was used.")]
    [InlineData(1234, "1,234 files have no photo date; their modified date was used.")]
    public void Missing_OneAndMany(int count, string line)
    {
        RenameItem[] items = Enumerable.Range(0, count).Select(i => ListedFile("p" + i + ".jpg")).ToArray();

        var run = Run(items, TakenArgs());

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(line), run.Error);
        Assert.DoesNotContain("no photo date", run.Output);
        Assert.Equal(count, Cmd.Lines(run.Output).Length);
        Assert.Equal("p0.jpg -> 2024-03-04_p0.jpg", Cmd.Lines(run.Output)[0]);
    }

    [Fact]
    public void Missing_OnlyCountsTheFilesWithoutADate()
    {
        _fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Taken));
        _fake.Answer(PathOf("c.jpg"), PhotoRead.Failed);

        var run = Run(new[] { ListedFile("a.jpg"), ListedFile("b.jpg"), ListedFile("c.jpg"), ListedFile("d.txt") }, TakenArgs());

        Assert.Equal(Cmd.Written("3 files have no photo date; their modified date was used."), run.Error);
    }

    [Fact]
    public void Missing_WithQuiet()
    {
        var run = Run(new[] { ListedFile("a.jpg") }, "--date", "--date-source", "taken", "-q");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Output);
        Assert.Equal(Cmd.Written("1 file has no photo date; its modified date was used."), run.Error);
    }

    [Fact]
    public void Missing_OnlyPlannedFiles()
    {
        var run = Run(new[] { ListedFile("a.jpg"), ListedFile("b.jpg"), ListedFile("c.txt") }, TakenArgs("--mask", "a*"));

        Assert.Equal(Cmd.Written("1 file has no photo date; its modified date was used."), run.Error);
        Assert.Equal(1, _fake.Calls);
        Assert.Equal(1, _fake.CallsFor(PathOf("a.jpg")));
        Assert.Equal(0, _fake.CallsFor(PathOf("b.jpg")));
    }

    [Fact]
    public void Missing_IncludeFolders_FolderNotCounted()
    {
        _fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Taken));

        var run = Run(new[] { ListedFile("a.jpg"), Folder("sub") }, TakenArgs("--include-folders"));

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Error);
        Assert.Contains("sub -> 2024-03-04_sub", run.Output);

        var two = Run(new[] { ListedFile("b.jpg"), Folder("sub") }, TakenArgs("--include-folders"));

        Assert.Equal(Cmd.Written("1 file has no photo date; its modified date was used."), two.Error);
    }

    [Fact]
    public void NoFilesMatch_NoRead_NoLine()
    {
        var run = Run(new[] { ListedFile("a.jpg") }, TakenArgs("--mask", "*.zip"));

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(Strings.NoFilesMatch), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, _fake.Calls);
    }

    [Fact]
    public void OtherSource_NoRead()
    {
        var run = Run(new[] { ListedFile("a.jpg") }, "--date", "--dry-run");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "a.jpg -> 2024-03-04_a.jpg" }, Cmd.Lines(run.Output));
        Assert.Equal("", run.Error);
        Assert.Equal(0, _fake.Calls);
    }

    [Fact]
    public void NoRule_NoRead()
    {
        var run = Run(new[] { ListedFile("a.jpg") }, "--prefix", "x", "--dry-run");

        Assert.Equal("", run.Error);
        Assert.Equal(0, _fake.Calls);
    }

    [Fact]
    public void BuiltRulesDecide_NotThePanel()
    {
        var items = new[] { ListedFile("a.jpg") };
        var args = new[] { "apply", _temp.Files, "--date", "--date-source", "taken", "--dry-run" };

        // The panel says Taken, but the built rules hold no Photo date rule: nothing is read, nothing is said.
        var none = Cmd.Run(
            new FakeBatchRenamer(), _temp.Files, args, new FakeFileListSource(FileListing.Ok(items)),
            _ => new IRenameRule[] { new AutoDateRule(DateSource.Modified) },
            photoDates: new PhotoDates(_fake, PhotoDates.DefaultCapacity));

        Assert.Equal("", none.Error);
        Assert.Equal(0, _fake.Calls);
    }

    [Fact]
    public void Blocked_StillWarns()
    {
        _fake.Answer(PathOf("a.jpg"), PhotoRead.Found(Taken));

        // A separator that cannot be in a name blocks the batch; b.jpg also fell back to its modified date.
        var run = Run(new[] { ListedFile("a.jpg"), ListedFile("b.jpg") }, TakenArgs("--date-separator", ":"));

        Assert.Equal(2, run.Code);
        string[] lines = Cmd.Lines(run.Error);
        Assert.Equal("1 file has no photo date; its modified date was used.", lines[0]);
        Assert.True(lines.Length > 1);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void AfterSkippedFoldersLine()
    {
        var run = Run(FileListing.Ok(new[] { ListedFile("a.jpg") }, 1), TakenArgs("--recurse"));

        string[] lines = Cmd.Lines(run.Error);
        Assert.Equal(2, lines.Length);
        Assert.Equal(SkippedFoldersNotice.Text(1, Cmd.EnUs), lines[0]);
        Assert.Equal("1 file has no photo date; its modified date was used.", lines[1]);
    }

    [Fact]
    public void RealJpeg_OnDisk()
    {
        System.IO.File.WriteAllBytes(PathOf("p.jpg"), JpegBuilder.ValidFile(false));
        System.IO.File.WriteAllText(PathOf("t.jpg"), "not a photo");
        System.IO.File.SetLastWriteTime(PathOf("p.jpg"), Modified);
        System.IO.File.SetLastWriteTime(PathOf("t.jpg"), Modified);

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--date", "--date-source", "taken");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "2023-07-01_p.jpg", "2024-03-04_t.jpg" }, _temp.FileNames());
        Assert.Equal(Cmd.Written("1 file has no photo date; its modified date was used."), run.Error);
    }

    [Fact]
    public void SavePreset_Taken_LoadsAsPhotoDate()
    {
        var run = Cmd.Run(
            new FakeBatchRenamer(), _temp.Files,
            new[] { "apply", _temp.Files, "--date", "--date-source", "taken", "--save-preset", "P", "--dry-run" },
            new FakeFileListSource(FileListing.Ok(new[] { ListedFile("a.jpg") })),
            presetsFolder: _temp.Presets,
            photoDates: new PhotoDates(_fake, PhotoDates.DefaultCapacity));

        Assert.Equal(0, run.Code);
        var window = new RulePanelHostModel(RulePanelModels.CreateAll());
        window.ApplyPreset(new PresetStore(_temp.Presets).Load("P").Preset!);
        var panel = Assert.Single(window.Panels.OfType<AutoDatePanelModel>());
        Assert.True(panel.Enabled);
        Assert.Equal(DateSource.Taken, panel.Source);
        Assert.Equal("Photo date", NameShift.Ui.Rules.DateSourceText.For(panel.Source));
    }
}
