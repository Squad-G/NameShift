using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Disk;
using Xunit;

namespace NameShift.Cmd.Tests;

public sealed class ExitCodesTests
{
    private const string Item = @"C:\A\f0.txt";

    private static RenameRunResult Build(string name)
    {
        JournalWriteFailure afterFinish = CmdResults.Journal(JournalWritePhase.AfterFinish);
        ExecutionFailure missing = CmdResults.Failure(FailureReason.SourceMissing, Item);
        ExecutionFailure inUse = CmdResults.Failure(FailureReason.ItemInUse, Item, systemMessage: "In use.");
        ExecutionFailure failed = CmdResults.Failure(FailureReason.RenameFailed, Item, systemMessage: "Access is denied.");
        StrandedItem stranded = CmdResults.Stranded(@"C:\A\tmp.txt", Item);
        switch (name)
        {
            case "Succeeded":
                return CmdResults.Succeeded(2);
            case "Succeeded, journal AfterFinish":
                return CmdResults.Succeeded(2, afterFinish);
            case "Journal BeforeStart":
                return CmdResults.NotStarted("The disk is full.");
            case "Refused HasConflicts":
                return CmdResults.Failed(ExecutionOutcome.Refused, CmdResults.Failure(FailureReason.HasConflicts));
            case "Refused HasRuleErrors":
                return CmdResults.Failed(ExecutionOutcome.Refused, CmdResults.Failure(FailureReason.HasRuleErrors));
            case "Refused SourceMissing":
                return CmdResults.Failed(ExecutionOutcome.Refused, missing);
            case "Refused TargetInTheWay":
                return CmdResults.Failed(ExecutionOutcome.Refused, CmdResults.Failure(FailureReason.TargetInTheWay, Item, @"C:\A\x.txt"));
            case "Refused SourceMissing, journal AfterFinish":
                return CmdResults.Failed(ExecutionOutcome.Refused, missing, afterFinish);
            case "RolledBack ItemInUse":
                return CmdResults.Failed(ExecutionOutcome.RolledBack, inUse);
            case "RolledBack RenameFailed":
                return CmdResults.Failed(ExecutionOutcome.RolledBack, failed);
            case "RolledBack RenameFailed, journal AfterFinish":
                return CmdResults.Failed(ExecutionOutcome.RolledBack, failed, afterFinish);
            case "NeedsAttention":
                return CmdResults.NeedsAttention(failed, null, stranded);
            case "NeedsAttention, journal AfterFinish":
                return CmdResults.NeedsAttention(inUse, afterFinish, stranded);
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown case.");
        }
    }

    public static IEnumerable<object[]> Cases() =>
        new (string Name, ExitCode Code)[]
        {
            ("Succeeded", ExitCode.Success),
            ("Succeeded, journal AfterFinish", ExitCode.NeedsAttention),
            ("Journal BeforeStart", ExitCode.NotRenamed),
            ("Refused HasConflicts", ExitCode.NotRenamed),
            ("Refused HasRuleErrors", ExitCode.NotRenamed),
            ("Refused SourceMissing", ExitCode.NotRenamed),
            ("Refused TargetInTheWay", ExitCode.NotRenamed),
            ("Refused SourceMissing, journal AfterFinish", ExitCode.NotRenamed),
            ("RolledBack ItemInUse", ExitCode.NotRenamed),
            ("RolledBack RenameFailed", ExitCode.NotRenamed),
            ("RolledBack RenameFailed, journal AfterFinish", ExitCode.NotRenamed),
            ("NeedsAttention", ExitCode.NeedsAttention),
            ("NeedsAttention, journal AfterFinish", ExitCode.NeedsAttention),
        }.Select(c => new object[] { c.Name, c.Code });

    [Theory]
    [MemberData(nameof(Cases))]
    public void For(string name, ExitCode expected)
    {
        Assert.Equal(expected, ExitCodes.For(Build(name)));
    }

    [Fact]
    public void For_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExitCodes.For((RenameRunResult)null!));
    }

    private static RevertResult BuildRevert(string name)
    {
        switch (name)
        {
            case "NothingToRevert":
                return CmdResults.NothingToRevert();
            case "Reverted":
                return CmdResults.Reverted(2);
            case "Reverted, journal AfterFinish":
                return CmdResults.Reverted(2, afterFinishIssue: true);
            case "PartlyReverted":
                return CmdResults.PartlyReverted(1, 1);
            case "NothingReverted":
                return CmdResults.NothingReverted(2);
            case "NothingReverted, journal AfterFinish":
                return CmdResults.NothingReverted(2, afterFinishIssue: true);
            case "PartlyReverted, journal AfterFinish":
                return CmdResults.PartlyReverted(1, 1, afterFinishIssue: true);
            case "NotStarted":
                return CmdResults.RevertNotStarted("The disk is full.");
            case "NeedsAttention":
                return CmdResults.RevertNeedsAttention();
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown case.");
        }
    }

    public static IEnumerable<object[]> RevertCases() =>
        new (string Name, ExitCode Code)[]
        {
            ("NothingToRevert", ExitCode.Success),
            ("Reverted", ExitCode.Success),
            ("Reverted, journal AfterFinish", ExitCode.NeedsAttention),
            ("PartlyReverted", ExitCode.NeedsAttention),
            ("NothingReverted", ExitCode.NotRenamed),
            ("NothingReverted, journal AfterFinish", ExitCode.NotRenamed),
            ("PartlyReverted, journal AfterFinish", ExitCode.NeedsAttention),
            ("NotStarted", ExitCode.NotRenamed),
            ("NeedsAttention", ExitCode.NeedsAttention),
        }.Select(c => new object[] { c.Name, c.Code });

    [Theory]
    [MemberData(nameof(RevertCases))]
    public void ForRevert(string name, ExitCode expected)
    {
        Assert.Equal(expected, ExitCodes.For(BuildRevert(name)));
    }

    [Fact]
    public void ForRevert_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ExitCodes.For((RevertResult)null!));
    }

    [Fact]
    public void Values_AreTheDocumentedNumbers()
    {
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, Enum.GetValues(typeof(ExitCode)).Cast<int>().ToArray());
        Assert.Equal(3, (int)ExitCode.NotRenamed);
        Assert.Equal(4, (int)ExitCode.NeedsAttention);
    }
}
