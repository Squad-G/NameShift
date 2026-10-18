using System.IO;
using Xunit;

namespace NameShift.Disk.Tests;

public sealed class UndoJournalFormatTests : JournalTestBase
{
    private const string Time = "2026-10-18T14:03:07.1234567+00:00";

    /// <summary>A journal with quotes written as ' and the given members after version, id and state.</summary>
    private static string Journal(string body, int version = 1, string state = "done") =>
        $"{{'version': {version}, 'id': 'id9', 'state': '{state}', {body}}}".Replace('\'', '"');

    private static string Entry(string kind, string oldPath, string newPath) =>
        $"{{'kind': '{kind}', 'old': '{Escape(oldPath)}', 'new': '{Escape(newPath)}'}}";

    private static string Escape(string path) => path.Replace(@"\", @"\\");

    private string ValidBody(string? extra = null) =>
        $"'time': '{Time}', 'folders': ['{Escape(Temp.Path)}'], 'renames': [{Entry("file", Temp.Full("a"), Temp.Full("b"))}]{extra}";

    private void WriteJournal(string text)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(LastBatchPath, text);
    }

    [Fact]
    public void Load_Missing_IsNone()
    {
        var last = Renamer.LoadLastBatch();

        Assert.Equal(LastBatchState.None, last.State);
        Assert.False(last.CanRevert);
        Assert.Null(last.Time);
        Assert.False(Directory.Exists(Root));
    }

    [Fact]
    public void Load_Valid_IsRevertable()
    {
        WriteJournal(Journal(ValidBody()));

        var last = Renamer.LoadLastBatch();

        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.Equal(Now, last.Time);
        Assert.Equal([(Temp.Full("a"), Temp.Full("b"))], Pairs(last.Renames));
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("empty")]
    [InlineData("version 2")]
    [InlineData("relative path")]
    [InlineData("unknown kind")]
    [InlineData("null renames")]
    [InlineData("missing renames")]
    [InlineData("null entry")]
    [InlineData("duplicate new paths")]
    [InlineData("done with no entry")]
    [InlineData("reverted with entries")]
    [InlineData("unknown state")]
    [InlineData("bad time")]
    [InlineData("same old and new")]
    [InlineData("version as text")]
    public void Load_Corrupt_IsUnreadableAndKeepsTheFile(string corruption)
    {
        var a = Temp.Full("a");
        var b = Temp.Full("b");
        var text = corruption switch
        {
            "garbage" => "\u0001\u0002{{ not json",
            "empty" => string.Empty,
            "version 2" => Journal(ValidBody(), version: 2),
            "relative path" => Journal($"'time': '{Time}', 'folders': [], 'renames': [{Entry("file", "a", b)}]".Replace('\'', '"')),
            "unknown kind" => Journal($"'time': '{Time}', 'folders': [], 'renames': [{Entry("link", a, b)}]"),
            "null renames" => Journal($"'time': '{Time}', 'folders': [], 'renames': null"),
            "missing renames" => Journal($"'time': '{Time}', 'folders': []"),
            "null entry" => Journal($"'time': '{Time}', 'folders': [], 'renames': [null]"),
            "duplicate new paths" => Journal(
                $"'time': '{Time}', 'folders': [], 'renames': [{Entry("file", a, b)}, {Entry("file", Temp.Full("c"), Temp.Full("B"))}]"),
            "done with no entry" => Journal($"'time': '{Time}', 'folders': [], 'renames': []"),
            "reverted with entries" => Journal(ValidBody(), state: "reverted"),
            "unknown state" => Journal(ValidBody(), state: "half"),
            "bad time" => Journal(ValidBody().Replace(Time, "yesterday")),
            "same old and new" => Journal($"'time': '{Time}', 'folders': [], 'renames': [{Entry("file", a, a)}]"),
            _ => Journal(ValidBody()).Replace("\"version\": 1", "\"version\": \"one\""),
        };
        WriteJournal(text.Replace('\'', '"'));
        var bytes = JournalBytes();
        var written = File.GetLastWriteTimeUtc(LastBatchPath);

        var last = Renamer.LoadLastBatch();
        var revert = Renamer.Revert();

        Assert.Equal(LastBatchState.Unreadable, last.State);
        Assert.False(last.CanRevert);
        Assert.Equal(RevertOutcome.NothingToRevert, revert.Outcome);
        Assert.Empty(Disk.Moves);
        Assert.Empty(Files.Writes);
        Assert.Empty(Files.Deletes);
        Assert.Equal(bytes, JournalBytes());
        Assert.Equal(written, File.GetLastWriteTimeUtc(LastBatchPath));
        Assert.Equal(["last-batch.json"], HistoryListing());
    }

    [Fact]
    public void Load_FileLockedByAnotherProcess_IsUnreadable()
    {
        RenameFiles(("a", "b"));

        using (new FileStream(LastBatchPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Equal(LastBatchState.Unreadable, Renamer.LoadLastBatch().State);
            Assert.Equal(RevertOutcome.NothingToRevert, Renamer.Revert().Outcome);
        }

        Assert.Equal(LastBatchState.Revertable, Renamer.LoadLastBatch().State);
    }

    [Fact]
    public void Load_UnknownExtraMember_IsAccepted()
    {
        WriteJournal(Journal(ValidBody(", 'fingerprints': [{'size': 12}]")));

        var last = Renamer.LoadLastBatch();

        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.Single(last.Renames);
    }
}
