using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Presets;
using Xunit;
using Xunit.Abstractions;

namespace NameShift.Cmd.Tests;

/// <summary><c>apply --recurse</c> through <see cref="CommandRunner"/>: over a fake listing for what is planned and
/// shown, over real temp trees (and a temp history root) for what is renamed. A tilde in an expected text stands for a
/// backslash, so that no escape sequence is written in this file.</summary>
public sealed class CommandRunnerRecurseTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();
    private readonly ITestOutputHelper _log;

    public CommandRunnerRecurseTests(ITestOutputHelper log)
    {
        _log = log;
    }

    public void Dispose() => _temp.Dispose();

    private static string Real(string text) => text.Replace('~', (char)92);

    private static string[] Real(params string[] texts) => texts.Select(Real).ToArray();

    private RenameItem File(string name, string subFolder = "", ItemAttributes attributes = ItemAttributes.None) =>
        new RenameItem(name, subFolder.Length == 0 ? _temp.Files : Path.Combine(_temp.Files, Real(subFolder)), ItemKind.File, attributes);

    private RenameItem Folder(string name, string subFolder = "", ItemAttributes attributes = ItemAttributes.None) =>
        new RenameItem(name, subFolder.Length == 0 ? _temp.Files : Path.Combine(_temp.Files, Real(subFolder)), ItemKind.Folder, attributes);

    private (int Code, string[] Lines, string Error, FakeBatchRenamer Renamer, FakeFileListSource Source) Plan(
        FileListing listing, params string[] options)
    {
        var renamer = new FakeBatchRenamer();
        var source = new FakeFileListSource(listing);
        var args = new[] { "apply", _temp.Files, "--prefix", "x", "--dry-run" }.Concat(options).ToArray();
        var run = Cmd.Run(renamer, _temp.Files, args, source);
        return (run.Code, Cmd.Lines(run.Output), run.Error, renamer, source);
    }

    private (int Code, string[] Lines, string Error, FakeBatchRenamer Renamer, FakeFileListSource Source) Plan(
        RenameItem[] items, params string[] options) => Plan(FileListing.Ok(items), options);

    private void Make(string relative)
    {
        string path = Path.Combine(_temp.Files, Real(relative));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, "content of " + relative);
    }

    private static string[] Entries(string folder) =>
        Directory.GetFileSystemEntries(folder, "*", SearchOption.AllDirectories)
            .Select(p => p.Substring(folder.Length + 1).Replace((char)92, '~'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

    // ---- AC 1: the listing, the tree order ----

    [Fact]
    public void Recurse_ListsSubfolders()
    {
        var run = Plan(new[] { File("a.txt") }, "--recurse");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { true }, run.Source.IncludeSubfolders);
    }

    [Fact]
    public void NoRecurse_ListsTheFolderAlone()
    {
        var run = Plan(new[] { File("a.txt") });

        Assert.Equal(new[] { false }, run.Source.IncludeSubfolders);
    }

    [Fact]
    public void Recurse_TreeOrder_OnDisk()
    {
        Make("a.txt");
        Make("sub~b.txt");
        Make("sub2~c.txt");
        Make("sub~deep~d.txt");

        var dry = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--recurse", "--prefix", "x", "--dry-run");

        Assert.Equal(0, dry.Code);
        Assert.Equal(
            Real("a.txt -> xa.txt", "sub~b.txt -> xb.txt", "sub~deep~d.txt -> xd.txt", "sub2~c.txt -> xc.txt"),
            Cmd.Lines(dry.Output));
        Assert.Equal("", dry.Error);

        var real = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--recurse", "--prefix", "x");

        Assert.Equal(0, real.Code);
        Assert.Equal(new[] { "sub", "sub2", "sub2~xc.txt", "sub~deep", "sub~deep~xd.txt", "sub~xb.txt", "xa.txt" }, Entries(_temp.Files));
    }

    // ---- AC 2: reparse points ----

    [Fact]
    public void Junction_NotEntered_OnDisk()
    {
        Make("real~f.txt");
        string link = Path.Combine(_temp.Files, "link");
        if (!TryMakeJunction(link, Path.Combine(_temp.Files, "real")))
        {
            return;
        }

        try
        {
            var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--recurse", "--prefix", "x", "--dry-run");

            Assert.Equal(0, run.Code);
            Assert.Equal(Real("real~f.txt -> xf.txt"), Assert.Single(Cmd.Lines(run.Output)));
            Assert.Equal("", run.Error);
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    /// <summary>A junction made by <c>cmd /c mklink /J</c> (no administrator rights needed). When it cannot be made the
    /// test returns early, with the reason in the test output (xUnit 2 cannot skip dynamically).</summary>
    private bool TryMakeJunction(string link, string target)
    {
        var start = new ProcessStartInfo("cmd.exe", "/c mklink /J \"" + link + "\" \"" + target + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using Process process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        bool finished = process.WaitForExit(10000);
        string said = output.Result + error.Result;
        bool made = finished && process.ExitCode == 0 && Directory.Exists(link)
            && (System.IO.File.GetAttributes(link) & FileAttributes.ReparsePoint) != 0;
        if (!made)
        {
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }

            _log.WriteLine("Skipped: mklink /J failed: " + said);
        }

        return made;
    }

    // ---- AC 3: relative names ----

    [Fact]
    public void Recurse_RelativeNames_DryRun()
    {
        var run = Plan(new[] { File("a.txt"), File("b.txt", "sub"), File("c.txt", "sub~deep") }, "--recurse");

        Assert.Equal(0, run.Code);
        Assert.Equal(Real("a.txt -> xa.txt", "sub~b.txt -> xb.txt", "sub~deep~c.txt -> xc.txt"), run.Lines);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void Recurse_RelativeNames_Unchanged_Verbose()
    {
        var renamer = new FakeBatchRenamer();
        var files = new FakeFileListSource(FileListing.Ok(new[] { File("b.txt", "sub") }));

        var run = Cmd.Run(renamer, _temp.Files,
            new[] { "apply", _temp.Files, "--recurse", "--case", "lower", "--verbose", "--dry-run" }, files);

        Assert.Equal(0, run.Code);
        Assert.Equal(
            new[] { string.Format(Cmd.EnUs, Strings.UnchangedLine, Real("sub~b.txt")), Strings.NothingToRename },
            Cmd.Lines(run.Output));
    }

    [Fact]
    public void Recurse_RelativeNames_Blocked()
    {
        var renamer = new FakeBatchRenamer();
        var files = new FakeFileListSource(FileListing.Ok(new[] { File("b.txt", "sub"), File("c.txt", "sub") }));

        var run = Cmd.Run(renamer, _temp.Files,
            new[] { "apply", _temp.Files, "--recurse", "--name-fixed", "same" }, files);

        Assert.Equal((int)ExitCode.Blocked, run.Code);
        Assert.Equal(0, renamer.RenameCalls);
        string[] lines = Cmd.Lines(run.Error);
        Assert.True(lines.Length >= 3, run.Error);
        Assert.StartsWith(Real("sub~b.txt: "), lines[0], StringComparison.Ordinal);
        Assert.StartsWith(Real("sub~c.txt: "), lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void NoRecurse_PlainNames()
    {
        // The fake lists an item below the root whatever the switch says; without --recurse its line is the bare name.
        var run = Plan(new[] { File("a.txt"), File("b.txt", "sub") });

        Assert.Equal(new[] { "a.txt -> xa.txt", "b.txt -> xb.txt" }, run.Lines);
    }

    [Fact]
    public void Recurse_OnlyRootItems_LinesAsWithoutIt()
    {
        var items = new[] { File("a.txt"), File("b.txt") };

        Assert.Equal(Plan(items).Lines, Plan(items, "--recurse").Lines);
    }

    // ---- AC 4: conflicts per folder ----

    [Fact]
    public void Recurse_SameNameInTwoFolders_NoConflict()
    {
        var run = Plan(new[] { File("x1.txt"), File("x2.txt", "sub") }, "--recurse", "--name-fixed", "x");

        Assert.Equal(0, run.Code);
        Assert.Equal(Real("x1.txt -> xx.txt", "sub~x2.txt -> xx.txt"), run.Lines);
    }

    [Fact]
    public void Recurse_SameNameInOneFolder_Conflict()
    {
        var run = Plan(new[] { File("x1.txt"), File("x2.txt") }, "--recurse", "--name-fixed", "x");

        Assert.Equal((int)ExitCode.Blocked, run.Code);
        Assert.Empty(run.Lines);
    }

    // ---- AC 5: skipped folders ----

    private (int Code, string[] Lines) Combined(FileListing listing, params string[] options)
    {
        var both = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(both, both, _temp.Files, new FakeBatchRenamer(), new FakeFileListSource(listing),
            Cmd.EnUs, "1.2.3", new PresetStore(Cmd.UnusedPresetsFolder()));
        int code = runner.Run(new[] { "apply", _temp.Files }.Concat(options).ToArray());
        return (code, Cmd.Lines(both.ToString()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Recurse_Skipped_One_And_Two(int skipped)
    {
        var run = Plan(FileListing.Ok(new[] { File("a.txt") }, skipped), "--recurse");

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(SkippedFoldersNotice.Text(skipped, Cmd.EnUs)!), run.Error);
        Assert.Equal(new[] { "a.txt -> xa.txt" }, run.Lines);

        // One writer for both streams: the notice is the first line, before the result lines.
        var together = Combined(FileListing.Ok(new[] { File("a.txt") }, skipped), "--recurse", "--prefix", "x", "--dry-run");
        Assert.Equal(new[] { SkippedFoldersNotice.Text(skipped, Cmd.EnUs)!, "a.txt -> xa.txt" }, together.Lines);
    }

    [Fact]
    public void Recurse_Skipped_WithQuiet()
    {
        var renamer = new FakeBatchRenamer();
        var files = new FakeFileListSource(FileListing.Ok(new[] { File("a.txt") }, 2));

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--recurse", "--prefix", "x", "-q" }, files);

        Assert.Equal(0, run.Code);
        Assert.Equal(1, renamer.RenameCalls);
        Assert.Equal(Cmd.Written(SkippedFoldersNotice.Text(2, Cmd.EnUs)!), run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void Recurse_Skipped_BeforeNoFilesMatch_And_NothingToRename()
    {
        string notice = SkippedFoldersNotice.Text(1, Cmd.EnUs)!;

        var noMatch = Combined(FileListing.Ok(new[] { File("a.txt") }, 1), "--recurse", "--prefix", "x", "--mask", "*.zip");
        Assert.Equal(0, noMatch.Code);
        Assert.Equal(new[] { notice, Strings.NoFilesMatch }, noMatch.Lines);

        var nothing = Combined(FileListing.Ok(new[] { File("a.txt") }, 1), "--recurse", "--case", "lower");
        Assert.Equal(0, nothing.Code);
        Assert.Equal(new[] { notice, Strings.NothingToRename }, nothing.Lines);

        var split = Plan(FileListing.Ok(new[] { File("a.txt") }, 1), "--recurse", "--mask", "*.zip");
        Assert.Equal(Cmd.Written(notice), split.Error);
        Assert.Equal(new[] { Strings.NoFilesMatch }, split.Lines);
    }

    [Fact]
    public void Recurse_Skipped_NoneOrNoRecurse_Silent()
    {
        var none = Plan(FileListing.Ok(new[] { File("a.txt") }, 0), "--recurse");
        var without = Plan(FileListing.Ok(new[] { File("a.txt") }, 2));

        Assert.Equal("", none.Error);
        Assert.Equal("", without.Error);
        Assert.Equal(0, without.Code);
    }

    // ---- AC 6: folders ----

    [Fact]
    public void Recurse_IncludeFolders_OnDisk_JournalAndUndo()
    {
        Make("Sub~f.txt");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--recurse", "--include-folders", "--prefix", "x");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "xSub", "xSub~xf.txt" }, Entries(_temp.Files));

        var undo = Cmd.RunOnDisk(_temp, "undo");

        Assert.Equal(0, undo.Code);
        Assert.Equal(new[] { "Sub", "Sub~f.txt" }, Entries(_temp.Files));
    }

    private RenameItem[] Tree() => new[]
    {
        Folder("A"), File("r.txt"), Folder("B", "A"), File("g.txt", "A"), File("h.txt", "A~B"),
    };

    [Fact]
    public void Recurse_FoldersOnly_EveryLevel()
    {
        var run = Plan(Tree(), "--recurse", "--folders-only");

        Assert.Equal(0, run.Code);
        Assert.Equal(Real("A -> xA", "A~B -> xB"), run.Lines);
    }

    [Fact]
    public void Recurse_RecurseAlone_FilesOnly()
    {
        var run = Plan(Tree(), "--recurse");

        Assert.Equal(Real("r.txt -> xr.txt", "A~g.txt -> xg.txt", "A~B~h.txt -> xh.txt"), run.Lines);
    }

    // ---- AC 7: filters ----

    private RenameItem[] Mixed() => new[]
    {
        File("a.jpg"), File("b.txt"), File("c.jpg", "sub"), File("d.txt", "sub"),
    };

    [Fact]
    public void Recurse_Filter_EveryFolder()
    {
        var run = Plan(Mixed(), "--recurse", "--mask", "*.jpg");

        Assert.Equal(Real("a.jpg -> xa.jpg", "sub~c.jpg -> xc.jpg"), run.Lines);

        var renamer = new FakeBatchRenamer();
        var files = new FakeFileListSource(FileListing.Ok(Mixed()));
        var viaFolder = Cmd.Run(renamer, _temp.Files,
            new[] { "apply", _temp.Files + (char)92 + "*.jpg", "--recurse", "--prefix", "x", "--dry-run" }, files);

        Assert.Equal(Real("a.jpg -> xa.jpg", "sub~c.jpg -> xc.jpg"), Cmd.Lines(viaFolder.Output));
    }

    [Fact]
    public void Recurse_HiddenFileInVisibleFolder()
    {
        var items = new[] { File("v.txt", "sub"), File("h.txt", "sub", ItemAttributes.Hidden) };

        Assert.Equal(Real("sub~v.txt -> xv.txt"), Assert.Single(Plan(items, "--recurse").Lines));
        Assert.Equal(
            Real("sub~v.txt -> xv.txt", "sub~h.txt -> xh.txt").OrderBy(l => l).ToArray(),
            Plan(items, "--recurse", "--include-hidden").Lines.OrderBy(l => l).ToArray());
    }

    [Fact]
    public void Recurse_NothingMatches_NoFilesMatch()
    {
        var run = Plan(Mixed(), "--recurse", "--mask", "*.zip");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { Strings.NoFilesMatch }, run.Lines);
    }

    // ---- AC 7a: hidden folders ----

    private RenameItem[] HiddenTree() => new[]
    {
        Folder("h", "", ItemAttributes.Hidden), Folder("p"), File("a.txt", "h"), File("b.txt", "p"),
    };

    [Fact]
    public void Recurse_HiddenFolder_LeftOut_IncludeHiddenBringsIt()
    {
        Assert.Equal(Real("p~b.txt -> xb.txt"), Assert.Single(Plan(HiddenTree(), "--recurse").Lines));

        Assert.Equal(
            Real("h~a.txt -> xa.txt", "p~b.txt -> xb.txt"),
            Plan(HiddenTree(), "--recurse", "--include-hidden").Lines);

        // Folders are items only when asked for; a hidden folder also needs --include-hidden.
        string[] withFolders = Plan(HiddenTree(), "--recurse", "--include-folders").Lines;
        Assert.Equal(Real("p -> xp", "p~b.txt -> xb.txt"), withFolders);

        string[] everything = Plan(HiddenTree(), "--recurse", "--include-folders", "--include-hidden").Lines;
        Assert.Equal(Real("h -> xh", "p -> xp", "h~a.txt -> xa.txt", "p~b.txt -> xb.txt"), everything);
    }

    [Fact]
    public void Recurse_SystemFolder_LeftOut()
    {
        var items = new[] { Folder("s", "", ItemAttributes.System), File("c.txt", "s"), File("d.txt") };

        Assert.Equal(new[] { "d.txt -> xd.txt" }, Plan(items, "--recurse").Lines);
    }

    [Fact]
    public void Recurse_OnlyHiddenFolderItems_Message()
    {
        var items = new[] { Folder("h", "", ItemAttributes.Hidden), File("a.txt", "h") };

        Assert.Equal(new[] { Strings.NothingToRename }, Plan(items, "--recurse").Lines);
        Assert.Equal(new[] { Strings.NoFilesMatch }, Plan(items, "--recurse", "--mask", "*.txt").Lines);
    }

    [Fact]
    public void Recurse_ChosenFolderHidden_StillWorks()
    {
        Make("a.txt");
        Make("sub~b.txt");
        System.IO.File.SetAttributes(_temp.Files, FileAttributes.Directory | FileAttributes.Hidden);

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--recurse", "--prefix", "x", "--dry-run");

        Assert.Equal(0, run.Code);
        Assert.Equal(Real("a.txt -> xa.txt", "sub~b.txt -> xb.txt"), Cmd.Lines(run.Output));
    }

    [Fact]
    public void Recurse_HiddenFolder_OnDisk()
    {
        Make(".git~config");
        Make("src~x.txt");
        string git = Path.Combine(_temp.Files, ".git");
        System.IO.File.SetAttributes(git, FileAttributes.Directory | FileAttributes.Hidden);

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--recurse", "--prefix", "x");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { ".git", ".git~config", "src", "src~xx.txt" }, Entries(_temp.Files));

        // --include-hidden brings the hidden folder's files in.
        var all = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--recurse", "--include-hidden", "--prefix", "y");

        Assert.Equal(0, all.Code);
        Assert.Equal(new[] { ".git", ".git~yconfig", "src", "src~yxx.txt" }, Entries(_temp.Files));
    }

    [Fact]
    public void Recurse_NoRecurse_Unchanged()
    {
        // Without --recurse the rule never applies: the hidden folder is an item like any other.
        var run = Plan(HiddenTree(), "--include-folders", "--include-hidden");

        Assert.Equal(
            new[] { "h -> xh", "p -> xp", "a.txt -> xa.txt", "b.txt -> xb.txt" },
            run.Lines);
    }

    // ---- AC 10: size ----

    [Fact]
    public void TenThousand_Fake_Completes()
    {
        var items = new List<RenameItem>();
        for (int folder = 0; folder < 100; folder++)
        {
            for (int file = 0; file < 100; file++)
            {
                items.Add(File("f" + file + ".txt", "d" + folder));
            }
        }

        var run = Plan(items.ToArray(), "--recurse");

        Assert.Equal(0, run.Code);
        Assert.Equal(10000, run.Lines.Length);
    }
}
