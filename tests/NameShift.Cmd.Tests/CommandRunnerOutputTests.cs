using System;
using System.IO;
using System.Linq;
using NameShift.Disk;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>What <c>apply</c> writes with --quiet, with --verbose and with neither. Only success output changes:
/// every failure is the same with and without the option. Disk tests use temp folders and the temp history root.</summary>
public sealed class CommandRunnerOutputTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    private static string[] Args(string joined) => joined.Split('|');

    /// <summary>The same command line without and with the option, on the same fake renamer result: the code, the
    /// output and the error must be equal.</summary>
    private void AssertSameWithOption(string option, FakeBatchRenamer renamer, int expectedCode, params string[] ruleArgs)
    {
        string[] plain = new[] { "apply", _temp.Files }.Concat(ruleArgs).ToArray();
        string[] with = plain.Concat(new[] { option }).ToArray();

        var without = Cmd.Run(renamer, _temp.Files, plain);
        var run = Cmd.Run(renamer, _temp.Files, with);

        Assert.Equal(expectedCode, without.Code);
        Assert.Equal(without.Code, run.Code);
        Assert.Equal(without.Output, run.Output);
        Assert.Equal(without.Error, run.Error);
    }

    private (RenameRunResult Result, int Code) Failure(string name)
    {
        string img1 = Path.Combine(_temp.Files, "IMG_001.JPG");
        string img2 = Path.Combine(_temp.Files, "IMG_002.JPG");
        switch (name)
        {
            case "Refused":
                return (CmdResults.Failed(ExecutionOutcome.Refused, CmdResults.Failure(FailureReason.SourceMissing, img2)), 3);
            case "NeedsAttention":
                return (CmdResults.NeedsAttention(
                    CmdResults.Failure(FailureReason.RenameFailed, img2, systemMessage: "Access is denied."),
                    null,
                    CmdResults.Stranded(Path.Combine(_temp.Files, "ns-tmp-1.JPG"), img1),
                    CmdResults.Stranded(Path.Combine(_temp.Files, "ns-tmp-2.JPG"), img2)), 4);
            case "AfterFinish":
                return (CmdResults.Succeeded(2, CmdResults.Journal(JournalWritePhase.AfterFinish)), 4);
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown failure.");
        }
    }

    private void ThreeImages()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        _temp.File("img_003.JPG");
    }

    [Fact]
    public void Quiet_Success_WritesNothing()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower", "-q");

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(new[] { "img_001.JPG", "img_002.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void Quiet_NothingToRename_WritesNothing()
    {
        _temp.File("abc.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "--quiet" });

        Assert.Equal(0, run.Code);
        Assert.Equal("", run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Fact]
    public void Quiet_Exit1_SameError()
    {
        string missing = Path.Combine(_temp.Root, "missing");

        var without = Cmd.RunOnDisk(_temp, "apply", missing, "--case", "lower");
        var run = Cmd.RunOnDisk(_temp, "apply", missing, "--case", "lower", "-q");

        Assert.Equal(1, run.Code);
        Assert.Equal(without, run);
        Assert.Single(Cmd.Lines(run.Error));
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void Quiet_Exit2_SameErrors()
    {
        _temp.File("b.txt");
        _temp.File("a.txt", FileAttributes.Hidden);

        var without = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "b", "a");
        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "b", "a", "-q");

        Assert.Equal(2, run.Code);
        Assert.Equal(without, run);
        Assert.Equal(new[] { "b.txt: " + UiStrings.ConflictTargetExists, UiStrings.RenameHintConflicts }, Cmd.Lines(run.Error));
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void Quiet_Exit3_SameErrors()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        (RenameRunResult result, int code) = Failure("Refused");
        var renamer = new FakeBatchRenamer { Result = result };

        AssertSameWithOption("-q", renamer, code, "--case", "lower");

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "-q" });
        Assert.NotEqual("", run.Error);
        Assert.Equal("", run.Output);
    }

    [Theory]
    [InlineData("NeedsAttention")]
    [InlineData("AfterFinish")]
    public void Quiet_Exit4_SameErrors(string name)
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        (RenameRunResult result, int code) = Failure(name);
        var renamer = new FakeBatchRenamer { Result = result };

        AssertSameWithOption("--quiet", renamer, code, "--case", "lower");

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "--quiet" });
        Assert.Equal(4, run.Code);
        Assert.NotEqual("", run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void Quiet_Crash_SameError()
    {
        _temp.File("IMG_001.JPG");
        var renamer = new FakeBatchRenamer { Throws = new InvalidOperationException("boom") };

        AssertSameWithOption("-q", renamer, 4, "--case", "lower");

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "-q" });
        Assert.Equal(Cmd.Written(UiStrings.ActionUnexpected), run.Error);
        Assert.Equal("", run.Output);
    }

    [Fact]
    public void Verbose_Rename_ListsRenamedThenUnchangedThenSummary()
    {
        ThreeImages();

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower", "-v");

        Assert.Equal(0, run.Code);
        Assert.Equal(
            Cmd.Written("IMG_001.JPG -> img_001.JPG", "IMG_002.JPG -> img_002.JPG", "img_003.JPG: unchanged", "2 files renamed."),
            run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(new[] { "img_001.JPG", "img_002.JPG", "img_003.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void Verbose_Rename_LinesComeFromThePlan()
    {
        ThreeImages();
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "-v" });

        Assert.Equal(0, run.Code);
        Assert.StartsWith(
            Cmd.Written("IMG_001.JPG -> img_001.JPG", "IMG_002.JPG -> img_002.JPG", "img_003.JPG: unchanged"),
            run.Output);
        Assert.Equal("", run.Error);
    }

    [Fact]
    public void Verbose_ListOrder_IsNatural()
    {
        _temp.File("file10.txt");
        _temp.File("file2.txt");
        _temp.File("File3.TXT");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "upper", "-v");

        Assert.Equal(0, run.Code);
        Assert.Equal(
            Cmd.Written("file2.txt -> FILE2.txt", "File3.TXT -> FILE3.TXT", "file10.txt -> FILE10.txt", "3 files renamed."),
            run.Output);
    }

    [Fact]
    public void Verbose_DryRun_ChangedThenUnchanged_NoSummary()
    {
        ThreeImages();

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower", "-v", "--dry-run");

        Assert.Equal(0, run.Code);
        Assert.Equal(
            Cmd.Written("IMG_001.JPG -> img_001.JPG", "IMG_002.JPG -> img_002.JPG", "img_003.JPG: unchanged"),
            run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(new[] { "IMG_001.JPG", "IMG_002.JPG", "img_003.JPG" }, _temp.FileNames());
    }

    [Fact]
    public void Verbose_NeverListsHiddenOrFolders()
    {
        _temp.File("h.txt", FileAttributes.Hidden);
        _temp.File("s.txt", FileAttributes.System);
        Directory.CreateDirectory(Path.Combine(_temp.Files, "Dir"));
        _temp.File("a.txt");

        var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--case", "lower", "-v", "--dry-run");

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("a.txt: unchanged", "Nothing to rename."), run.Output);
        Assert.Equal("", run.Error);
        Assert.DoesNotContain("h.txt", run.Output + run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("s.txt", run.Output + run.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Dir", run.Output + run.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void Verbose_NothingToRename_UnchangedFirst()
    {
        _temp.File("abc.txt");
        _temp.File("def.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "-v" });

        Assert.Equal(0, run.Code);
        Assert.Equal(Cmd.Written("abc.txt: unchanged", "def.txt: unchanged", "Nothing to rename."), run.Output);
        Assert.Equal("", run.Error);
        Assert.Equal(0, renamer.RenameCalls);
    }

    [Theory]
    [InlineData("dry")]
    [InlineData("real")]
    [InlineData("conflict")]
    [InlineData("nothing")]
    public void Default_Output_Unchanged(string scenario)
    {
        string[] args;
        string output;
        string error;
        int code;
        switch (scenario)
        {
            case "dry":
                ThreeImages();
                args = new[] { "apply", _temp.Files, "--case", "lower", "--dry-run" };
                output = "IMG_001.JPG -> img_001.JPG\r\nIMG_002.JPG -> img_002.JPG\r\n".Replace("\r\n", Environment.NewLine);
                error = "";
                code = 0;
                break;
            case "real":
                ThreeImages();
                args = new[] { "apply", _temp.Files, "--case", "lower" };
                output = "2 files renamed." + Environment.NewLine;
                error = "";
                code = 0;
                break;
            case "conflict":
                _temp.File("b.txt");
                _temp.File("a.txt", FileAttributes.Hidden);
                args = new[] { "apply", _temp.Files, "--replace", "b", "a" };
                output = "";
                error = "b.txt: An item with this name already exists in the folder: choose another name" + Environment.NewLine
                    + "Fix the conflicts to rename." + Environment.NewLine;
                code = 2;
                break;
            case "nothing":
                _temp.File("abc.txt");
                args = new[] { "apply", _temp.Files, "--case", "lower" };
                output = "Nothing to rename." + Environment.NewLine;
                error = "";
                code = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown scenario.");
        }

        var run = Cmd.RunOnDisk(_temp, args);

        Assert.Equal(code, run.Code);
        Assert.Equal(output, run.Output);
        Assert.Equal(error, run.Error);
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("Refused")]
    [InlineData("NeedsAttention")]
    [InlineData("AfterFinish")]
    public void Verbose_Failure_SameAsWithout(string name)
    {
        if (name == "conflict")
        {
            _temp.File("b.txt");
            _temp.File("a.txt", FileAttributes.Hidden);
            var without = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "b", "a");
            var run = Cmd.RunOnDisk(_temp, "apply", _temp.Files, "--replace", "b", "a", "-v");

            Assert.Equal(2, run.Code);
            Assert.Equal(without, run);
            Assert.Equal("", run.Output);
            return;
        }

        ThreeImages();
        (RenameRunResult result, int code) = Failure(name);
        var renamer = new FakeBatchRenamer { Result = result };

        var plain = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower" });
        var verbose = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower", "-v" });

        Assert.Equal(code, verbose.Code);
        Assert.Equal(plain.Code, verbose.Code);
        Assert.Equal(plain.Output, verbose.Output);
        Assert.Equal(plain.Error, verbose.Error);
        Assert.DoesNotContain(" -> ", verbose.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(": unchanged", verbose.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(" -> ", verbose.Error, StringComparison.Ordinal);
        Assert.DoesNotContain(": unchanged", verbose.Error, StringComparison.Ordinal);
    }

    [Fact]
    public void NeverReadsInput()
    {
        for (int i = 0; i < 501; i++)
        {
            _temp.File($"F{i:D3}.TXT");
        }

        TextReader original = Console.In;
        try
        {
            Console.SetIn(new ThrowingReader());
            string[][] commandLines =
            {
                new[] { "apply", _temp.Files, "--case", "lower", "-q" },
                new[] { "apply", _temp.Files, "--case", "lower", "-v" },
                new[] { "apply", _temp.Files, "--case", "lower", "-v", "--dry-run" },
                new[] { "apply", _temp.Files, "--case", "lower" },
            };

            foreach (string[] commandLine in commandLines)
            {
                var run = Cmd.Run(new FakeBatchRenamer { Result = CmdResults.Succeeded(501) }, _temp.Files, commandLine);

                Assert.Equal(0, run.Code);
                Assert.Equal("", run.Error);
            }
        }
        finally
        {
            Console.SetIn(original);
        }
    }

    private sealed class ThrowingReader : TextReader
    {
        public override int Read() => throw new InvalidOperationException("The command line must not read input.");

        public override int Peek() => throw new InvalidOperationException("The command line must not read input.");

        public override string ReadLine() => throw new InvalidOperationException("The command line must not read input.");

        public override string ReadToEnd() => throw new InvalidOperationException("The command line must not read input.");
    }
}
