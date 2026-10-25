using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using NameShift.Core;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;
using Xunit;
using UiStrings = NameShift.Ui.Strings;

namespace NameShift.Cmd.Tests;

/// <summary>What the runner gives the renamer, and how it reports each result, with a fake renamer (no file moves).</summary>
public sealed class CommandRunnerRenamerTests : IDisposable
{
    private readonly TempFolder _temp = new TempFolder();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Rename_GetsThePlannersOwnPlan()
    {
        _temp.File("IMG_001.JPG");
        _temp.File("img_002.jpeg");
        _temp.File("notes.txt");
        _temp.File("hidden IMG.txt", FileAttributes.Hidden);
        Directory.CreateDirectory(Path.Combine(_temp.Files, "IMG folder"));
        var renamer = new FakeBatchRenamer { Result = CmdResults.Succeeded(2) };

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--replace", "IMG", "Trip" });

        Assert.Equal(0, run.Code);
        Assert.Equal(1, renamer.RenameCalls);
        FileListing listing = new DiskFileListSource().ListEntries(_temp.Files, false, CancellationToken.None);
        RenameItem[] items = FileListOrder.Sort(listing.Items).Where(new ItemFilter(includeFolders: false).Includes).ToArray();
        RenamePlan expected = RenamePlanner.Plan(
            new IRenameRule[] { new ReplaceRule("IMG", "Trip") }, items, listing.ExistingNamesByFolder, CancellationToken.None);

        Assert.Equal(
            expected.Results.Select(r => (r.Item.Name, r.Item.FolderPath, r.NewName, r.Status)).ToArray(),
            renamer.Results!.Select(r => (r.Item.Name, r.Item.FolderPath, r.NewName, r.Status)).ToArray());
        Assert.Equal(new[] { "IMG_001.JPG", "img_002.jpeg", "notes.txt" }, renamer.Results!.Select(r => r.Item.Name).ToArray());
        Assert.Equal(expected.Conflicts.Count, renamer.Conflicts!.Count);
        for (int i = 0; i < expected.Conflicts.Count; i++)
        {
            Assert.Equal(expected.Conflicts.ForItem(i).Select(c => c.Code), renamer.Conflicts.ForItem(i).Select(c => c.Code));
        }
    }

    [Fact]
    public void Rename_SubfolderFilesLeftOut()
    {
        // NS-74 AC 2: a matching file in a sub-folder is never part of the batch (recursion is NS-41).
        _temp.File("IMG_001.JPG");
        string sub = Path.Combine(_temp.Files, "sub");
        Directory.CreateDirectory(sub);
        File.WriteAllText(Path.Combine(sub, "IMG_002.JPG"), "in a sub-folder");
        var renamer = new FakeBatchRenamer { Result = CmdResults.Succeeded(1) };

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--replace", "IMG", "Trip" });

        Assert.Equal(0, run.Code);
        Assert.Equal(new[] { ("IMG_001.JPG", _temp.Files) }, renamer.Results!.Select(r => (r.Item.Name, r.Item.FolderPath)).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Blocked_NeverCallsTheRenamer(bool dryRun)
    {
        _temp.File("a.txt");
        _temp.File("b.txt");
        var renamer = new FakeBatchRenamer();
        var args = new List<string> { "apply", _temp.Files, "--replace", "b", "a" };
        if (dryRun)
        {
            args.Add("--dry-run");
        }

        var run = Cmd.Run(renamer, _temp.Files, args);

        Assert.Equal(2, run.Code);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal("", run.Output);
        // a.txt keeps its name in the batch, so b.txt's new name duplicates another item's.
        Assert.Equal(new[] { "b.txt: " + UiStrings.ConflictDuplicateTarget, UiStrings.RenameHintConflicts }, Cmd.Lines(run.Error));
    }

    [Fact]
    public void Blocked_PrintsEveryProblem_AndTheHint()
    {
        _temp.File("a.txt");
        _temp.File("b.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(
            renamer,
            _temp.Files,
            new[] { "apply", _temp.Files, "--case", "lower" },
            buildRules: _ => new IRenameRule[] { new RenameToRule(0, "a?b.txt"), new ThrowingRule(RuleStage.Case, 1, "boom") });

        Assert.Equal(2, run.Code);
        Assert.Equal(0, renamer.RenameCalls);
        Assert.Equal("", run.Output);
        string conflict = ConflictText.For(new[] { new Conflict(ConflictCode.InvalidCharacter, "?") });
        Assert.Equal(
            new[]
            {
                "a.txt: " + conflict,
                "b.txt: Error: boom",
                "Fix the conflicts and rule errors to rename.",
            },
            Cmd.Lines(run.Error));
        Assert.Equal(string.Format(CultureInfo.InvariantCulture, UiStrings.ConflictInvalidCharacterOne, "?"), conflict);
    }

    [Fact]
    public void Blocked_RuleErrorOnly_SaysFixTheRuleErrors()
    {
        _temp.File("a.txt");
        var renamer = new FakeBatchRenamer();

        var run = Cmd.Run(
            renamer,
            _temp.Files,
            new[] { "apply", _temp.Files, "--case", "lower", "--dry-run" },
            buildRules: _ => new IRenameRule[] { new ThrowingRule(RuleStage.Case, 0, "line one\nline two") });

        Assert.Equal(2, run.Code);
        Assert.Equal(new[] { "a.txt: Error: line one line two", UiStrings.RenameHintErrors }, Cmd.Lines(run.Error));
    }

    public static IEnumerable<object[]> OutcomeNames() =>
        new[]
        {
            "Succeeded", "Refused SourceMissing", "RolledBack ItemInUse", "RolledBack RenameFailed", "Journal BeforeStart",
            "NeedsAttention", "Succeeded Journal AfterFinish",
        }.Select(name => new object[] { name });

    private static (RenameRunResult Result, int Code) Outcome(string name, string folder)
    {
        string img1 = Path.Combine(folder, "IMG_001.JPG");
        string img2 = Path.Combine(folder, "IMG_002.JPG");
        switch (name)
        {
            case "Succeeded":
                return (CmdResults.Succeeded(3), 0);
            case "Refused SourceMissing":
                return (CmdResults.Failed(ExecutionOutcome.Refused, CmdResults.Failure(FailureReason.SourceMissing, img2)), 3);
            case "RolledBack ItemInUse":
                return (CmdResults.Failed(
                    ExecutionOutcome.RolledBack,
                    CmdResults.Failure(FailureReason.ItemInUse, img1, systemMessage: "The process cannot access the file.")), 3);
            case "RolledBack RenameFailed":
                return (CmdResults.Failed(
                    ExecutionOutcome.RolledBack,
                    CmdResults.Failure(FailureReason.RenameFailed, img1, systemMessage: "Access is denied.")), 3);
            case "Journal BeforeStart":
                return (CmdResults.NotStarted("The disk is full."), 3);
            case "NeedsAttention":
                return (CmdResults.NeedsAttention(
                    CmdResults.Failure(FailureReason.RenameFailed, img2, systemMessage: "Access is denied."),
                    null,
                    CmdResults.Stranded(Path.Combine(folder, "ns-tmp-1.JPG"), img1),
                    CmdResults.Stranded(Path.Combine(folder, "ns-tmp-2.JPG"), img2)), 4);
            case "Succeeded Journal AfterFinish":
                return (CmdResults.Succeeded(2, CmdResults.Journal(JournalWritePhase.AfterFinish)), 4);
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown outcome.");
        }
    }

    [Theory]
    [MemberData(nameof(OutcomeNames))]
    public void Outcomes(string name)
    {
        _temp.File("IMG_001.JPG");
        _temp.File("IMG_002.JPG");
        (RenameRunResult result, int expectedCode) = Outcome(name, _temp.Files);
        var renamer = new FakeBatchRenamer { Result = result };

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower" });

        Assert.Equal(expectedCode, run.Code);
        Assert.Equal(1, renamer.RenameCalls);
        ActionReport report = ActionText.ForRename(result, Cmd.EnUs);
        string expected = report.Details == null ? Cmd.Written(report.Status) : Cmd.Written(report.Status, report.Details);
        if (expectedCode == 0)
        {
            Assert.Equal(expected, run.Output);
            Assert.Equal("", run.Error);
            Assert.Equal(Cmd.Written("3 files renamed."), run.Output);
        }
        else
        {
            Assert.Equal(expected, run.Error);
            Assert.Equal("", run.Output);
        }

        if (name == "NeedsAttention")
        {
            Assert.NotNull(report.Details);
            Assert.Contains(Path.Combine(_temp.Files, "ns-tmp-1.JPG"), run.Error, StringComparison.Ordinal);
            Assert.Contains(Path.Combine(_temp.Files, "ns-tmp-2.JPG"), run.Error, StringComparison.Ordinal);
            Assert.Contains("(was IMG_001.JPG)", run.Error, StringComparison.Ordinal);
            Assert.Contains("(was IMG_002.JPG)", run.Error, StringComparison.Ordinal);
        }

        if (name == "Journal BeforeStart")
        {
            Assert.StartsWith(string.Format(CultureInfo.InvariantCulture, UiStrings.RenameNoHistory, ""), run.Error, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RenamerThrows_Exit4_ActionUnexpected()
    {
        _temp.File("IMG_001.JPG");
        var renamer = new FakeBatchRenamer { Throws = new InvalidOperationException("A bug in the renamer.") };

        var run = Cmd.Run(renamer, _temp.Files, new[] { "apply", _temp.Files, "--case", "lower" });

        Assert.Equal(4, run.Code);
        Assert.Equal(1, renamer.RenameCalls);
        Assert.Equal(Cmd.Written(UiStrings.ActionUnexpected), run.Error);
        Assert.Equal("", run.Output);
    }
}
