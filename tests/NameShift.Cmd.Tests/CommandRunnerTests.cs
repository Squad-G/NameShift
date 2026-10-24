using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NameShift.Core;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>The command on real temp folders (each test its own folder and history root), and its input errors.</summary>
public sealed class CommandRunnerTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private static string Format(string format, params object[] values) =>
        string.Format(CultureInfo.InvariantCulture, format, values);

    private void AssertInputError((int Code, string Output, string Error) run, string expectedError, FakeBatchRenamer renamer)
    {
        Assert.Equal(1, run.Code);
        Assert.Equal(Cmd.Written(expectedError), run.Error);
        Assert.Single(Cmd.Lines(run.Error));
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.False(Directory.Exists(_temp.History));
    }

    [Fact]
    public void FolderErrors_MissingFolder()
    {
        _temp.File("IMG_001.JPG");
        string missing = Path.Combine(_temp.Files, "missing");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", missing, "--case", "lower" });

        AssertInputError(run, Format(Strings.FolderNotFound, missing), renamer);
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void FolderErrors_FilePath()
    {
        string file = _temp.File("IMG_001.JPG");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", file, "--case", "lower" });

        AssertInputError(run, Format(Strings.FolderIsFile, file), renamer);
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void FolderErrors_Quote()
    {
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", "D:\\Pics\"", "--case", "lower" });

        AssertInputError(
            run,
            "The folder D:\\Pics\" contains a quote: remove the backslash before the closing quote, as in \"D:\\Pics\" instead of \"D:\\Pics\\\".",
            renamer);
    }

    [Fact]
    public void FolderErrors_QuoteSwallowedTheOptions()
    {
        // What cmd passes for: NameShiftCmd apply "D:\Pics\" --case lower
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", "D:\\Pics\" --case lower" });

        AssertInputError(run, Format(Strings.QuoteInFolder, "D:\\Pics\" --case lower"), renamer);
    }

    [Fact]
    public void FolderErrors_InvalidCharacter()
    {
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", @"C:\a<b", "--case", "lower" });

        AssertInputError(run, Format(Strings.InvalidFolder, @"C:\a<b"), renamer);
    }

    [Fact]
    public void FolderErrors_RelativeInvalidCharacter()
    {
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", "a<b", "--case", "lower" });

        AssertInputError(run, Format(Strings.InvalidFolder, "a<b"), renamer);
    }

    [Fact]
    public void FolderErrors_RelativeFolder_IsJoinedToTheCurrentDirectory()
    {
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", "sub", "--case", "lower" });

        AssertInputError(run, Format(Strings.FolderNotFound, Path.Combine(_temp.Files, "sub")), renamer);
    }

    [Fact]
    public void RelativeFolder_Exists_IsUsed()
    {
        Directory.CreateDirectory(Path.Combine(_temp.Files, "sub"));
        File.WriteAllText(Path.Combine(_temp.Files, "sub", "A.txt"), "a");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", "sub", "--case", "lower", "--dry-run" });

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("A.txt -> a.txt"), run.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("  ")]
    public void BlankFolder_IsNoFolder_RenamesNothingInTheCurrentDirectory(string folder)
    {
        // What a script passes when its folder variable is empty: the current directory must not be renamed.
        _temp.File("IMG_001.JPG");

        var run = Cmd.RunOnDisk(_temp, "apply", folder, "--case", "lower");

        Assert.Equal(1, run.Code);
        Assert.Equal(Cmd.Written(Strings.NoFolder + " " + Strings.Usage + " " + Strings.HelpPointer), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
        Assert.False(Directory.Exists(_temp.History));
    }

    [Fact]
    public void ReplaceMissingItsNewText_DoesNotSwallowDryRun_RenamesNothing()
    {
        _temp.File("IMG_001.JPG");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "IMG", "--dry-run");

        Assert.Equal(1, run.Code);
        Assert.Equal(Cmd.Written(Strings.ReplaceNeedsValues + " " + Strings.Usage + " " + Strings.HelpPointer), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
        Assert.False(Directory.Exists(_temp.History));
    }

    [Fact]
    public void FolderVariables_AreKeptAsTyped()
    {
        // A folder literally named %TEMP% under the current directory: the command line never expands variables.
        string literal = Path.Combine(_temp.Files, "%TEMP%");
        Directory.CreateDirectory(literal);
        File.WriteAllText(Path.Combine(literal, "A.txt"), "a");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", "%TEMP%", "--case", "lower", "--dry-run" });

        Assert.Equal("", run.Error);
        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("A.txt -> a.txt"), run.Output);
    }

    [Fact]
    public void Listing_DefaultFilter_KeepsVisibleFilesOnly()
    {
        // The listing itself may hand over hidden, system and folder items (NS-33 changes it): the filter drops them.
        var items = new[]
        {
            new RenameItem("HIDDEN.txt", _temp.Files, ItemKind.File, ItemAttributes.Hidden),
            new RenameItem("SYSTEM.txt", _temp.Files, ItemKind.File, ItemAttributes.System),
            new RenameItem("FOLDER", _temp.Files, ItemKind.Folder),
            new RenameItem("READONLY.txt", _temp.Files, ItemKind.File, ItemAttributes.ReadOnly),
            new RenameItem("VISIBLE.txt", _temp.Files),
        };
        var files = new FakeFileListSource(FileListing.Ok(items));
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "--dry-run" }, files);

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "READONLY.txt -> readonly.txt", "VISIBLE.txt -> visible.txt" }, Cmd.Lines(run.Output));
    }

    [Fact]
    public void ListingThrows_Exit3_NothingRenamed()
    {
        var renamer = new FakeBatchRenamer();
        var files = new ThrowingFileListSource();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower" }, files);

        Assert.Equal(3, run.Code);
        Assert.Equal(Cmd.Written(Strings.UnexpectedBeforeRename), run.Error);
        Assert.Equal("", run.Output);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void OutputFails_AfterTheRename_Exit4()
    {
        // e.g. a closed pipe: the batch ran, the report could not be written; the exit code stays in 0..4.
        _temp.File("IMG_001.JPG");
        var renamer = new FakeBatchRenamer { Result = CmdResults.Succeeded(1) };
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(new ThrowingWriter(), error, _temp.Files, renamer, new DiskFileListSource(), Cmd.EnUs, "1.2.3");

        int code = runner.Run(new[] { "apply", _temp.Files, "--case", "lower" });

        Assert.Equal(4, code);
        Assert.Equal(1, renamer.RenameCalls);
        Assert.Equal(Cmd.Written(UiStrings.ActionUnexpected), error.ToString());
    }

    [Fact]
    public void BothStreamsFail_ListingThrows_Exit3()
    {
        var runner = new CommandRunner(
            new ThrowingWriter(), new ThrowingWriter(), _temp.Files, new FakeBatchRenamer(), new ThrowingFileListSource(), Cmd.EnUs, "1.2.3");

        Assert.Equal(3, runner.Run(new[] { "apply", _temp.Files, "--case", "lower" }));
    }

    [Fact]
    public void RenamerThrows_ErrorStreamFails_StillExit4()
    {
        _temp.File("IMG_001.JPG");
        var renamer = new FakeBatchRenamer { Throws = new InvalidOperationException("A bug in the renamer.") };
        var runner = new CommandRunner(
            new StringWriter(CultureInfo.InvariantCulture), new ThrowingWriter(), _temp.Files, renamer, new DiskFileListSource(), Cmd.EnUs, "1.2.3");

        Assert.Equal(4, runner.Run(new[] { "apply", _temp.Files, "--case", "lower" }));
        Assert.Equal(1, renamer.RenameCalls);
    }

    [Fact]
    public void DryRunOutputFails_Exit3()
    {
        // A dry run never calls the renamer, so a failing output stream cannot have renamed anything.
        _temp.File("IMG_001.JPG");
        var renamer = new FakeBatchRenamer();
        var error = new StringWriter(CultureInfo.InvariantCulture);
        var runner = new CommandRunner(new ThrowingWriter(), error, _temp.Files, renamer, new DiskFileListSource(), Cmd.EnUs, "1.2.3");

        int code = runner.Run(new[] { "apply", _temp.Files, "--case", "lower", "--dry-run" });

        Assert.Equal(3, code);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal(Cmd.Written(Strings.UnexpectedBeforeRename), error.ToString());
        Assert.Equal(new[] { "IMG_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void Crash_NeverPrintsTheExceptionText()
    {
        var listing = Cmd.Run(new FakeBatchRenamer(), _temp.Files, new[] { "apply", _temp.Files, "--case", "lower" }, new ThrowingFileListSource());
        _temp.File("IMG_001.JPG");
        var renaming = Cmd.Run(
            new FakeBatchRenamer { Throws = new InvalidOperationException("A bug in the listing.") },
            _temp.Files,
            new[] { "apply", _temp.Files, "--case", "lower" });

        foreach (string text in new[] { listing.Output, listing.Error, renaming.Output, renaming.Error })
        {
            Assert.DoesNotContain("A bug in the listing.", text, StringComparison.Ordinal);
            Assert.DoesNotContain("   at ", text, StringComparison.Ordinal);
        }
    }

    private sealed class ThrowingFileListSource : IFileListSource
    {
        public FileListing ListEntries(string folderPath) => throw new InvalidOperationException("A bug in the listing.");
    }

    private sealed class ThrowingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw new IOException("The pipe has been ended.");

        public override void WriteLine(string? value) => throw new IOException("The pipe has been ended.");
    }

    [Fact]
    public void UnreadableFolder_IsAnInputError()
    {
        var renamer = new FakeBatchRenamer();
        var files = new FakeFileListSource(FileListing.Failed(FolderProblem.AccessDenied));

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower" }, files);

        AssertInputError(run, Format(Strings.CannotList, _temp.Files, UiStrings.AccessDenied), renamer);
        Assert.Contains(UiStrings.AccessDenied, run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageError_RunnerWritesOneLine()
    {
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply" });

        AssertInputError(run, Strings.NoFolder + " " + Strings.Usage + " " + Strings.HelpPointer, renamer);
    }

    [Fact]
    public void CaseLower_RenamesBoth_Says2FilesRenamed()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower");

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("2 files renamed."), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(new[] { "img_001.JPG", "img_002.JPG" }, _temp.FileNames());
        Assert.True(File.Exists(Path.Combine(_temp.History, "last-batch.json")), "No journal under " + _temp.History);
    }

    [Fact]
    public void OneFile_Says1FileRenamed()
    {
        _temp.File("IMG_001.JPG");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower");

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("1 file renamed."), run.Output);
        Assert.Equal(new[] { "img_001.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void Replace_IgnoresCase_LeavesTheExtension()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("photo.img");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "img", "Trip");

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("1 file renamed."), run.Output);
        Assert.Equal(new[] { "Trip_001.JPG", "photo.img" }, _temp.FileNames());
    }

    [Fact]
    public void OptionOrder_DoesNotMatter()
    {
        using var other = new TempFolder();
        foreach (TempFolder folder in new[] { _temp, other })
        {
            folder.File("ax_1.txt");
            folder.File("bx.TXT");
        }

        var first = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "x", "y", "--case", "upper");
        var second = Cmd.RunOnDisk(other, "apply", "--case", "upper", "--replace", "x", "y", other.Files);

        Assert.Equal(0, first.Code);
        Assert.Equal(0, second.Code);
        Assert.Equal(new[] { "AY_1.txt", "BY.TXT" }, _temp.FileNames());
        Assert.Equal(_temp.FileNames(), other.FileNames());
    }

    [Fact]
    public void DryRun_PrintsOldToNew_TouchesNothing()
    {
        string[] paths = { _temp.File("IMG_001.JPG"), _temp.File("IMG_002.JPG"), _temp.File("notes.txt") };
        DateTime old = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        foreach (string path in paths)
        {
            File.SetLastWriteTimeUtc(path, old);
        }

        Directory.SetLastWriteTimeUtc(_temp.Files, old);

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower", "--dry-run");

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { "IMG_001.JPG -> img_001.JPG", "IMG_002.JPG -> img_002.JPG" }, Cmd.Lines(run.Output));
        Assert.Equal("", run.Error);
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG", "notes.txt" }, _temp.FileNames());
        foreach (string path in paths)
        {
            Assert.Equal(old, File.GetLastWriteTimeUtc(path));
        }

        Assert.Equal(old, Directory.GetLastWriteTimeUtc(_temp.Files));
        Assert.False(Directory.Exists(_temp.History));
    }

    [Fact]
    public void DryRun_NeverCallsTheRenamer()
    {
        _temp.File("IMG_001.JPG");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "--dry-run" });

        Assert.Equal(0, run.Code);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal(Cmd.Written("IMG_001.JPG -> img_001.JPG"), run.Output);
    }

    [Fact]
    public void FilesOnly_InNaturalOrder()
    {
        _temp.File("file10.txt");
        _temp.File("file2.txt");
        Directory.CreateDirectory(Path.Combine(_temp.Files, "FileDir"));

        var dry = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "upper", "--dry-run");

        Assert.Equal(0, dry.Code);
        Assert.Equal(new[] { "file2.txt -> FILE2.txt", "file10.txt -> FILE10.txt" }, Cmd.Lines(dry.Output));

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "upper");

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("2 files renamed."), run.Output);
        Assert.Equal(new[] { "FILE10.txt", "FILE2.txt" }, _temp.FileNames());
        Assert.Equal("FileDir", Path.GetFileName(Assert.Single(Directory.GetDirectories(_temp.Files))));
    }

    [Fact]
    public void HiddenFile_NotRenamed_ButCountsForDuplicates()
    {
        _temp.File("b.txt");
        string hidden = _temp.File("a.txt", FileAttributes.Hidden);

        var blocked = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "b", "a");

        Assert.Equal(2, blocked.Code);
        Assert.Equal(new[] { "b.txt: " + UiStrings.ConflictTargetExists, UiStrings.RenameHintConflicts }, Cmd.Lines(blocked.Error));
        Assert.Equal("", blocked.Output);
        Assert.Equal(new[] { "a.txt", "b.txt" }, _temp.FileNames());
        Assert.False(Directory.Exists(_temp.History));

        var upper = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "upper");

        Assert.Equal(0, upper.Code);
        Assert.Equal(Cmd.Written("1 file renamed."), upper.Output);
        Assert.Equal(new[] { "B.txt", "a.txt" }, _temp.FileNames());
        Assert.True((File.GetAttributes(hidden) & FileAttributes.Hidden) != 0);
    }

    [Fact]
    public void NothingToRename_Exit0()
    {
        _temp.File("abc.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower" });

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(Strings.NothingToRename), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void NothingToRename_DryRun_Exit0()
    {
        _temp.File("abc.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "--dry-run" });

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written(Strings.NothingToRename), run.Output);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void ManyFiles_NeverAsks()
    {
        // The runner has no input parameter: it cannot prompt. 501 files pass the window's 500-file confirmation size.
        for (int i = 0; i < 501; i++)
        {
            _temp.File("f" + i.ToString("D3", CultureInfo.InvariantCulture) + ".TXT");
        }

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "upper");

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("501 files renamed."), run.Output);
        Assert.Equal("", run.Error);
        Assert.All(_temp.FileNames(), name => Assert.StartsWith("F", name, StringComparison.Ordinal));
    }

    [Fact]
    public void Constructor_RejectsNulls()
    {
        var output = new StringWriter(CultureInfo.InvariantCulture);
        var renamer = new FakeBatchRenamer();
        var files = new DiskFileListSource();
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(null!, output, "C:\\", renamer, files, Cmd.EnUs, "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(output, null!, "C:\\", renamer, files, Cmd.EnUs, "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(output, output, null!, renamer, files, Cmd.EnUs, "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(output, output, "C:\\", null!, files, Cmd.EnUs, "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(output, output, "C:\\", renamer, null!, Cmd.EnUs, "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(output, output, "C:\\", renamer, files, null!, "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(output, output, "C:\\", renamer, files, Cmd.EnUs, null!));
        Assert.Throws<ArgumentNullException>(() => new CommandRunner(output, output, "C:\\", renamer, files, Cmd.EnUs, "1.2.3").Run(null!));
    }
}
