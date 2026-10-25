using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>NS-90: CheckRevert and Revert compare each file with its fingerprint, on real temp folders.</summary>
public sealed class CheckRevertTests : JournalTestBase
{
    /// <summary>The skips as (old name, reason, blocking name or null), in journal order.</summary>
    private static (string Old, RevertSkipReason Reason, string? Blocking)[] Skips(IEnumerable<SkippedRevert> skipped) =>
        skipped
            .Select(skip => (
                Path.GetFileName(skip.Entry.OldPath),
                skip.Reason,
                skip.BlockingPath == null ? null : Path.GetFileName(skip.BlockingPath)))
            .ToArray();

    private void Edit(string name) => File.AppendAllText(Temp.Full(name), " edited");

    // AC 3: one reason per entry, ItemMissing > NameTaken > Modified > WaitsForSkippedItem.

    [Theory]
    [InlineData("changed and missing")]
    [InlineData("changed and name taken")]
    [InlineData("changed and waiting")]
    [InlineData("waiting behind a changed file")]
    [InlineData("missing")]
    [InlineData("name taken")]
    [InlineData("changed")]
    [InlineData("waiting behind a taken name")]
    [InlineData("changed by one tick, same size")]
    public void Precedence(string row)
    {
        (string Old, RevertSkipReason Reason, string? Blocking)[] expected;
        switch (row)
        {
            case "changed and missing":
                // Edited, then replaced by a folder: no file to read (a null fingerprint), but missing first.
                RenameFiles(("a", "x"));
                Edit("x");
                File.Delete(Temp.Full("x"));
                Temp.Folder("x");
                expected = [("a", RevertSkipReason.ItemMissing, null)];
                break;
            case "changed and name taken":
                RenameFiles(("a", "x"));
                Edit("x");
                Temp.File("a", "newcomer");
                expected = [("a", RevertSkipReason.NameTaken, "a")];
                break;
            case "changed and waiting":
                // The chain a -> b -> c: c goes back to b only after b goes back to a, and a is taken.
                RenameFiles(("a", "b"), ("b", "c"));
                Edit("c");
                Temp.File("a", "newcomer");
                expected = [("b", RevertSkipReason.Modified, null), ("a", RevertSkipReason.NameTaken, "a")];
                break;
            case "waiting behind a changed file":
                RenameFiles(("a", "b"), ("b", "c"));
                Edit("b");
                expected = [("b", RevertSkipReason.WaitsForSkippedItem, "b"), ("a", RevertSkipReason.Modified, null)];
                break;
            case "missing":
                RenameFiles(("a", "x"), ("c", "z"));
                File.Delete(Temp.Full("x"));
                expected = [("a", RevertSkipReason.ItemMissing, null)];
                break;
            case "name taken":
                RenameFiles(("a", "x"), ("c", "z"));
                Temp.File("a", "newcomer");
                expected = [("a", RevertSkipReason.NameTaken, "a")];
                break;
            case "changed":
                RenameFiles(("a", "x"), ("c", "z"));
                Edit("x");
                expected = [("a", RevertSkipReason.Modified, null)];
                break;
            case "waiting behind a taken name":
                RenameFiles(("a", "b"), ("b", "c"));
                Temp.File("a", "newcomer");
                expected = [("b", RevertSkipReason.WaitsForSkippedItem, "b"), ("a", RevertSkipReason.NameTaken, "a")];
                break;
            default:
                // Same size, last write time 100 ns later: an exact comparison sees it.
                RenameFiles(("a", "x"), ("c", "z"));
                File.SetLastWriteTimeUtc(Temp.Full("x"), File.GetLastWriteTimeUtc(Temp.Full("x")).AddTicks(1));
                expected = [("a", RevertSkipReason.Modified, null)];
                break;
        }

        var check = Renamer.CheckRevert();

        Assert.Equal(expected, Skips(check.Skipped));
        Assert.Equal(expected.Any(skip => skip.Reason == RevertSkipReason.Modified), check.AnyModified);

        // Revert skips the same entries for the same reasons.
        var result = Renamer.Revert();
        Assert.Equal(expected, Skips(result.NotReverted));
        Assert.Equal(check.AnyRevertable, result.Reverted.Count > 0);
    }

    [Fact]
    public void CheckRevert_ReadOnly_NeverThrows()
    {
        RenameFiles(("a", "x"), ("b", "y"), ("c", "z"));
        Edit("x");
        File.Delete(Temp.Full("y"));
        Temp.File("c", "newcomer");
        var before = Snapshot();
        var moves = Disk.Moves.Count;
        var writes = Files.Writes.Count;
        var deletes = Files.Deletes.Count;

        var check = Renamer.CheckRevert();
        var again = Renamer.CheckRevert();

        Assert.Equal(
            [("a", RevertSkipReason.Modified, null), ("b", RevertSkipReason.ItemMissing, null), ("c", RevertSkipReason.NameTaken, "c")],
            Skips(check.Skipped));
        Assert.True(check.AnyModified);
        Assert.False(check.AnyRevertable);
        Assert.Equal(Skips(check.Skipped), Skips(again.Skipped));
        Assert.Equal(before, Snapshot());
        Assert.Equal(moves, Disk.Moves.Count);
        Assert.Equal(writes, Files.Writes.Count);
        Assert.Equal(deletes, Files.Deletes.Count);

        Disk.FailExists = _ => new IOException("The device is not ready.");
        Assert.Same(RevertCheck.Empty, Renamer.CheckRevert());
        Assert.Equal(before, Snapshot());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("reverted")]
    [InlineData("interrupted")]
    [InlineData("unreadable")]
    public void CheckRevert_NotRevertable_IsEmpty(string state)
    {
        switch (state)
        {
            case "none":
                break;
            case "reverted":
                RenameFiles(("a", "x"));
                Assert.Equal(RevertOutcome.Reverted, Renamer.Revert().Outcome);
                break;
            case "interrupted":
                RenameFiles(("a", "x"));
                Edit("x");
                File.WriteAllText(PendingPath, "garbage");
                break;
            default:
                RenameFiles(("a", "x"));
                Edit("x");
                File.WriteAllText(LastBatchPath, "garbage");
                break;
        }

        var before = Snapshot();

        var check = Renamer.CheckRevert();

        Assert.Empty(check.Skipped);
        Assert.False(check.AnyModified);
        Assert.False(check.AnyRevertable);
        Assert.Equal(before, Snapshot());
    }

    /// <summary>Every entry of the temp folder and of the history folder, with its size, last write time and attributes.</summary>
    private IReadOnlyList<string> Snapshot() =>
        new[] { Temp.Path, History.Path }
            .SelectMany(folder => Directory.GetFileSystemEntries(folder, "*", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                var size = File.Exists(path) ? new FileInfo(path).Length : -1;
                return $"{path}|{size}|{File.GetLastWriteTimeUtc(path).Ticks}|{File.GetAttributes(path)}";
            })
            .ToList();

    // AC 4

    [Fact]
    public void Edited_IsModified()
    {
        RenameFiles(("a", "x"), ("b", "y"));
        Edit("x");

        var check = Renamer.CheckRevert();

        var skip = Assert.Single(check.Skipped);
        Assert.Equal((Temp.Full("x"), RevertSkipReason.Modified, (string?)null), (skip.Entry.NewPath, skip.Reason, skip.BlockingPath));
        Assert.True(check.AnyModified);
        Assert.True(check.AnyRevertable);
    }

    [Fact]
    public void OnlyRead_NotModified()
    {
        RenameFiles(("a", "x"));
        var written = File.GetLastWriteTimeUtc(Temp.Full("x"));

        Assert.Equal("a", File.ReadAllText(Temp.Full("x")));
        using (var stream = new FileStream(Temp.Full("x"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.Equal((int)'a', stream.ReadByte());
        }

        // NTFS does not change the last write time on a read.
        Assert.Equal(written, File.GetLastWriteTimeUtc(Temp.Full("x")));
        var check = Renamer.CheckRevert();
        Assert.Empty(check.Skipped);
        Assert.False(check.AnyModified);
        Assert.True(check.AnyRevertable);
        Assert.Equal(RevertOutcome.Reverted, Renamer.Revert().Outcome);
    }

    [Fact]
    public void RenamedBackByHand_IsItemMissing()
    {
        RenameFiles(("a", "x"));
        File.Move(Temp.Full("x"), Temp.Full("a"));

        var skip = Assert.Single(Renamer.CheckRevert().Skipped);

        Assert.Equal((RevertSkipReason.ItemMissing, (string?)null), (skip.Reason, skip.BlockingPath));
        Assert.Equal(Temp.Full("x"), skip.Entry.NewPath);
    }

    [Fact]
    public void MissingWithFingerprint_ItemMissingOnly()
    {
        RenameFiles(("a", "x"));
        Assert.NotNull(Assert.Single(Renamer.LoadLastBatch().Renames).Fingerprint);
        File.Delete(Temp.Full("x"));
        Disk.FingerprintReads.Clear();

        var check = Renamer.CheckRevert();

        var skip = Assert.Single(check.Skipped);
        Assert.Equal((RevertSkipReason.ItemMissing, (string?)null), (skip.Reason, skip.BlockingPath));
        Assert.False(check.AnyModified);
        Assert.False(check.AnyRevertable);
        Assert.Empty(Disk.FingerprintReads);
    }

    [Fact]
    public void UnreadableButPresent_IsModified()
    {
        RenameFiles(("a", "x"));
        Disk.HideFingerprint = path => path == Temp.Full("x");

        var skip = Assert.Single(Renamer.CheckRevert().Skipped);

        Assert.Equal(RevertSkipReason.Modified, skip.Reason);
    }

    // AC 5

    [Fact]
    public void Revert_SkipsModified_KeepsItRevertable()
    {
        RenameFiles(("a", "x"), ("b", "y"));
        var fingerprint = Renamer.LoadLastBatch().Renames.Single(rename => rename.NewPath == Temp.Full("x")).Fingerprint;
        Edit("x");

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        Assert.Equal([(Temp.Full("y"), Temp.Full("b"))], Pairs(result.Reverted));
        var skip = Assert.Single(result.NotReverted);
        Assert.Equal((Temp.Full("x"), RevertSkipReason.Modified, (string?)null), (skip.Entry.NewPath, skip.Reason, skip.BlockingPath));
        Assert.Equal(["b", "x"], Temp.Listing());
        Assert.Equal("a edited", Temp.Content("x"));
        Assert.DoesNotContain(Disk.Moves, move => move.From == Temp.Full("x"));
        var last = Renamer.LoadLastBatch();
        Assert.Equal(LastBatchState.Revertable, last.State);
        var kept = Assert.Single(last.Renames);
        Assert.Equal((Temp.Full("a"), Temp.Full("x")), (kept.OldPath, kept.NewPath));
        Assert.Equal(fingerprint, kept.Fingerprint);
    }

    // AC 6

    [Fact]
    public void Revert_IncludeModified_RevertsIt_NeverOverwrites()
    {
        RenameFiles(("a", "x"), ("b", "y"));
        Edit("x");
        Temp.File("b", "newcomer");

        var result = Renamer.Revert(includeModified: true);

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        Assert.Equal([(Temp.Full("x"), Temp.Full("a"))], Pairs(result.Reverted));
        Assert.Equal([("b", RevertSkipReason.NameTaken, "b")], Skips(result.NotReverted));
        Assert.Equal(["a", "b", "y"], Temp.Listing());
        Assert.Equal("a edited", Temp.Content("a"));
        Assert.Equal("newcomer", Temp.Content("b"));
        Assert.DoesNotContain(Disk.Moves, move => move.To == Temp.Full("b"));
    }

    [Fact]
    public void Revert_IncludeModified_MissingStaysSkipped()
    {
        RenameFiles(("a", "x"), ("b", "y"));
        Edit("x");
        File.Delete(Temp.Full("y"));

        var result = Renamer.Revert(includeModified: true);

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        Assert.Equal([("b", RevertSkipReason.ItemMissing, null)], Skips(result.NotReverted));
        Assert.Equal(["a"], Temp.Listing());
    }

    // AC 7

    [Theory]
    [InlineData("chain")]
    [InlineData("swap")]
    [InlineData("case only")]
    [InlineData("folder and an item inside")]
    public void ChainSwapCaseOnly_WithFingerprints(string scenario)
    {
        using var withFingerprints = new Scenario();
        using var without = new Scenario();
        without.Disk.HideFingerprint = _ => true;

        var reverted = withFingerprints.RunAndRevert(scenario);
        var baseline = without.RunAndRevert(scenario);

        Assert.Equal(RevertOutcome.Reverted, reverted.Outcome);
        Assert.Equal(baseline.Moves, reverted.Moves);
        Assert.Equal(baseline.Listing, reverted.Listing);
        Assert.Contains(withFingerprints.Journal, rename => rename.Fingerprint != null);
        Assert.All(withFingerprints.Journal.Where(rename => rename.Kind == ItemKind.Folder), rename => Assert.Null(rename.Fingerprint));
        Assert.All(without.Journal, rename => Assert.Null(rename.Fingerprint));
    }

    [Fact]
    public void FolderEntry_NeverModified()
    {
        Temp.Folder("D");
        Temp.File(Path.Combine("D", "f.txt"));
        Run(Changed(0, "D", "E", ItemKind.Folder));
        Temp.File(Path.Combine("E", "new.txt"), "changes the folder's last write time");

        var check = Renamer.CheckRevert();

        Assert.Empty(check.Skipped);
        Assert.Equal(RevertOutcome.Reverted, Renamer.Revert().Outcome);
        Assert.Equal(["D"], Temp.Listing());
    }

    /// <summary>One batch and its revert in a fresh pair of temp folders.</summary>
    private sealed class Scenario : JournalTestBase
    {
        public IReadOnlyList<CompletedRename> Journal { get; private set; } = [];

        public (RevertOutcome Outcome, List<(string, string)> Moves, string Listing) RunAndRevert(string scenario)
        {
            switch (scenario)
            {
                case "chain":
                    RenameFiles(("a", "b"), ("b", "c"));
                    break;
                case "swap":
                    RenameFiles(("a", "b"), ("b", "a"));
                    break;
                case "case only":
                    RenameFiles(("photo.jpg", "Photo.jpg"));
                    break;
                default:
                    Temp.Folder("X");
                    Temp.File(Path.Combine("X", "a.txt"));
                    Assert.Equal(
                        ExecutionOutcome.Succeeded,
                        Run(Changed(0, "X", "Y", ItemKind.Folder), Changed(1, "a.txt", "b.txt", sub: "X")).Execution!.Outcome);
                    break;
            }

            Journal = Renamer.LoadLastBatch().Renames;
            Disk.Moves.Clear();
            var result = Renamer.Revert();
            var moves = Disk.Moves.Select(move => (Relative(move.From), Relative(move.To))).ToList();
            var listing = string.Join(
                "|",
                Directory.GetFileSystemEntries(Temp.Path, "*", SearchOption.AllDirectories)
                    .Select(path => Relative(path) + "=" + (File.Exists(path) ? File.ReadAllText(path) : "dir"))
                    .OrderBy(entry => entry, StringComparer.Ordinal));
            return (result.Outcome, moves, listing);
        }

        private string Relative(string path) => path.Substring(Temp.Path.Length);
    }

    // AC 9

    [Fact]
    public void PartialRevert_KeepsFingerprints()
    {
        RenameFiles(("a", "x"), ("b", "y"));
        var entry = EntryText(Temp.Full("x"));
        Edit("x");

        Assert.Equal(RevertOutcome.PartlyReverted, Renamer.Revert().Outcome);

        Assert.Equal(entry, EntryText(Temp.Full("x")));
        Assert.Contains("\"size\": 1", entry);
        Assert.Contains("\"modified\"", entry);
        Assert.Equal([("a", RevertSkipReason.Modified, null)], Skips(NewRenamer().CheckRevert().Skipped));
        var second = Renamer.Revert();
        Assert.Equal(RevertOutcome.NothingReverted, second.Outcome);
        Assert.Equal([("a", RevertSkipReason.Modified, null)], Skips(second.NotReverted));
        Assert.Equal(RevertOutcome.Reverted, Renamer.Revert(includeModified: true).Outcome);
        Assert.Equal(["a", "b"], Temp.Listing());
    }

    [Fact]
    public void PartialRevert_UnderARevertedFolder_KeepsFingerprints()
    {
        Temp.Folder("X");
        Temp.File(Path.Combine("X", "a.txt"));
        Run(Changed(0, "X", "Y", ItemKind.Folder), Changed(1, "a.txt", "b.txt", sub: "X"));
        var fingerprint = Renamer.LoadLastBatch().Renames.Single(rename => rename.Kind == ItemKind.File).Fingerprint;
        Assert.NotNull(fingerprint);
        Edit(Path.Combine("Y", "b.txt"));

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.PartlyReverted, result.Outcome);
        Assert.Equal(["X"], Temp.Listing());
        var kept = Assert.Single(Renamer.LoadLastBatch().Renames);
        Assert.Equal((Temp.Full(Path.Combine("X", "a.txt")), Temp.Full(Path.Combine("X", "b.txt"))), (kept.OldPath, kept.NewPath));
        Assert.Equal(fingerprint, kept.Fingerprint);
        Assert.Contains("\"size\"", EntryText(Temp.Full(Path.Combine("X", "b.txt"))));
        var skip = Assert.Single(Renamer.CheckRevert().Skipped);
        Assert.Equal((Temp.Full(Path.Combine("X", "b.txt")), RevertSkipReason.Modified), (skip.Entry.NewPath, skip.Reason));
        Assert.Equal(RevertSkipReason.Modified, Assert.Single(Renamer.Revert().NotReverted).Reason);
    }

    /// <summary>The JSON object of the journal entry whose new path is the given one.</summary>
    private string EntryText(string newPath)
    {
        var text = Encoding.UTF8.GetString(JournalBytes());
        var escaped = newPath.Replace(@"\", @"\\");
        return Regex.Matches(text, "\\{[^{}]*\\}")
            .Cast<Match>()
            .Single(match => match.Value.Contains("\"new\": \"" + escaped + "\""))
            .Value;
    }

    [Fact]
    public void ZeroByteFile_BatchLoadCheckRevert_RoundTrips()
    {
        Temp.File("a", "");
        Temp.File("c", "");
        RenameFiles(("a", "x"), ("c", "z"));

        // A restart of the app: the journal is read from disk, then checked.
        var restarted = NewRenamer();
        Assert.Equal(LastBatchState.Revertable, restarted.LoadLastBatch().State);
        var check = restarted.CheckRevert();

        Assert.Empty(check.Skipped);
        Assert.True(check.AnyRevertable);
        Assert.False(check.AnyModified);

        var result = restarted.Revert();

        Assert.Equal(RevertOutcome.Reverted, result.Outcome);
        Assert.Equal(2, result.Reverted.Count);
        Assert.Equal(new[] { "a", "c" }, Directory.GetFiles(Temp.Path).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        Assert.All(Directory.GetFiles(Temp.Path), path => Assert.Equal(0, new FileInfo(path).Length));
    }
}
