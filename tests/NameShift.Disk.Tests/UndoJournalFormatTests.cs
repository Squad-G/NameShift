using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

    // NS-91 AC 8: the journal reader's depth limit (JsonReaderQuotas, MaxDepth 32).

    /// <summary><paramref name="levels"/> nested containers, the innermost one empty: objects {"d":{"d":...{}...}} or
    /// arrays [[...[]...]]. How the reader counts (probed, the same as the settings and preset readers): each "{" or
    /// "[" from the root is one level, the root object being level 1, and a scalar value is one level more than the
    /// container holding it.</summary>
    private static string Nested(int levels, bool arrays = false) => arrays
        ? new string('[', levels) + new string(']', levels)
        : string.Concat(Enumerable.Repeat("{\"d\":", levels - 1)) + "{}" + new string('}', levels - 1);

    /// <summary>A valid last-batch.json whose unknown member "deep" makes the whole document <paramref name="depth"/>
    /// levels deep (the root object is level 1, so "deep" holds depth - 1 containers).</summary>
    private string DeepJournal(int depth, bool arrays = false) =>
        Journal(ValidBody()).TrimEnd('}') + ", \"deep\": " + Nested(depth - 1, arrays) + "}";

    [Fact]
    public void Depth32_Reads_Depth33_Unreadable()
    {
        Assert.Equal(32, JsonReaderQuotas.MaxDepth);
        foreach (bool arrays in new[] { false, true })
        {
            string at32 = DeepJournal(32, arrays);
            string at33 = DeepJournal(33, arrays);

            Assert.Equal("id9", JournalJson.ParseLastBatch(Encoding.UTF8.GetBytes(at32)).Id);
            WriteJournal(at32);
            Assert.Equal(LastBatchState.Revertable, Renamer.LoadLastBatch().State);

            Assert.ThrowsAny<Exception>(() => JournalJson.ParseLastBatch(Encoding.UTF8.GetBytes(at33)));
            WriteJournal(at33);
            Assert.Equal(LastBatchState.Unreadable, Renamer.LoadLastBatch().State);
        }
    }

    [Fact]
    public void PendingDepth33_Unreadable()
    {
        static byte[] Pending(int depth) =>
            Encoding.UTF8.GetBytes("{\"version\": 1, \"id\": \"p1\", \"deep\": " + Nested(depth - 1) + "}");

        Assert.Equal("p1", JournalJson.ParsePendingId(Pending(32)));
        Assert.ThrowsAny<Exception>(() => JournalJson.ParsePendingId(Pending(33)));
    }

    [Fact]
    public async Task DeepNesting_Unreadable_Within5s()
    {
        WriteJournal(string.Concat(Enumerable.Repeat("{\"a\":", 100000)));
        Assert.True(new FileInfo(LastBatchPath).Length < 1024 * 1024);

        Task<LastBatch> load = Task.Run(() => Renamer.LoadLastBatch());
        Task first = await Task.WhenAny(load, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.True(first == load, "LoadLastBatch did not finish in 5 seconds.");
        Assert.Equal(LastBatchState.Unreadable, (await load).State);
    }

    [Fact]
    public void RealJournal_StillLoads()
    {
        RenameFiles(("a", "b"), ("c", "d"));

        var record = JournalJson.ParseLastBatch(JournalBytes());
        var last = NewRenamer().LoadLastBatch();

        Assert.Equal(2, record.Renames.Count);
        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.Equal([(Temp.Full("a"), Temp.Full("b")), (Temp.Full("c"), Temp.Full("d"))], Pairs(last.Renames));
    }
}
