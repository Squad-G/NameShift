using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using NameShift.Core;
using Xunit;

namespace NameShift.Disk.Tests;

/// <summary>NS-90: each file entry of last-batch.json holds the file's size and last write time after the batch, in a
/// format that old journals and old readers both still understand.</summary>
public sealed class FingerprintTests : JournalTestBase
{
    private const string Time = "2026-10-18T14:03:07.1234567+00:00";

    private static string Escape(string path) => path.Replace(@"\", @"\\");

    /// <summary>The JSON objects of the journal's entries (an entry holds no nested object), keyed by their "new" path.</summary>
    private Dictionary<string, string> EntryTexts()
    {
        var text = Encoding.UTF8.GetString(JournalBytes());
        return Regex.Matches(text, "\\{[^{}]*\\}")
            .Cast<Match>()
            .Select(match => match.Value)
            .ToDictionary(entry => Regex.Match(entry, "\"new\": \"([^\"]*)\"").Groups[1].Value, entry => entry);
    }

    private static string ModifiedText(string path) =>
        File.GetLastWriteTimeUtc(path).ToString("o", CultureInfo.InvariantCulture);

    /// <summary>A last-batch.json with the given entry objects (quotes written as ').</summary>
    private void WriteJournal(params string[] entries)
    {
        Directory.CreateDirectory(Root);
        var text = $"{{'version': 1, 'id': 'id9', 'state': 'done', 'time': '{Time}', 'folders': ['{Escape(Temp.Path)}'], "
            + $"'renames': [{string.Join(", ", entries)}]}}";
        File.WriteAllText(LastBatchPath, text.Replace('\'', '"'));
    }

    private static string Entry(string kind, string oldPath, string newPath, string extra = "") =>
        $"{{'kind': '{kind}', 'old': '{Escape(oldPath)}', 'new': '{Escape(newPath)}'{extra}}}";

    // AC 1

    [Fact]
    public void SucceededBatch_WritesSizeAndModified()
    {
        Temp.File("a", "four");
        Temp.File("b", "seven!!");
        Temp.Folder("D");

        var result = Run(Changed(0, "a", "x"), Changed(1, "b", "y"), Changed(2, "D", "E", ItemKind.Folder));

        Assert.Equal(ExecutionOutcome.Succeeded, result.Execution!.Outcome);
        Assert.Null(result.JournalIssue);
        Assert.All(result.Execution.Renamed, rename => Assert.Null(rename.Fingerprint));
        var text = Encoding.UTF8.GetString(JournalBytes());
        Assert.Contains("\"version\": 1", text);
        var entries = EntryTexts();
        Assert.Equal(3, entries.Count);
        foreach (var (name, size) in new[] { ("x", 4), ("y", 7) })
        {
            var entry = entries[Escape(Temp.Full(name))];
            Assert.Contains($"\"size\": {size}", entry);
            Assert.Contains($"\"modified\": \"{ModifiedText(Temp.Full(name))}\"", entry);
            Assert.EndsWith("Z\"", Regex.Match(entry, "\"modified\": \"[^\"]*\"").Value);
        }

        var folder = entries[Escape(Temp.Full("E"))];
        Assert.DoesNotContain("size", folder);
        Assert.DoesNotContain("modified", folder);

        var last = Renamer.LoadLastBatch();
        var x = last.Renames.Single(rename => rename.NewPath == Temp.Full("x"));
        Assert.Equal(4, x.Fingerprint!.Size);
        Assert.Equal(File.GetLastWriteTimeUtc(Temp.Full("x")).Ticks, x.Fingerprint.ModifiedUtc.Ticks);
        Assert.Equal(DateTimeKind.Utc, x.Fingerprint.ModifiedUtc.Kind);
        Assert.Null(last.Renames.Single(rename => rename.Kind == ItemKind.Folder).Fingerprint);
    }

    [Fact]
    public void SucceededBatch_UnreadableFile_NoMembers_BatchSucceeds()
    {
        Disk.HideFingerprint = path => path == Temp.Full("y");

        RenameFiles(("a", "x"), ("b", "y"));

        var entries = EntryTexts();
        Assert.Contains("\"size\"", entries[Escape(Temp.Full("x"))]);
        Assert.DoesNotContain("size", entries[Escape(Temp.Full("y"))]);
        Assert.DoesNotContain("modified", entries[Escape(Temp.Full("y"))]);
        var last = Renamer.LoadLastBatch();
        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.Null(last.Renames.Single(rename => rename.NewPath == Temp.Full("y")).Fingerprint);
        Assert.NotNull(last.Renames.Single(rename => rename.NewPath == Temp.Full("x")).Fingerprint);
    }

    [Fact]
    public void SucceededBatch_OneStatPerFile_NoneForFolders()
    {
        Temp.Folder("D");
        Temp.File("a");
        Temp.File("b");

        Run(Changed(0, "a", "x"), Changed(1, "b", "y"), Changed(2, "D", "E", ItemKind.Folder));

        Assert.Equal([Temp.Full("x"), Temp.Full("y")], Disk.FingerprintReads.OrderBy(path => path, StringComparer.Ordinal));
    }

    // AC 2

    [Fact]
    public void OldJournal_LoadsAndReverts()
    {
        Temp.File("b", "a");
        Temp.File("d", "c");
        WriteJournal(Entry("file", Temp.Full("a"), Temp.Full("b")), Entry("file", Temp.Full("c"), Temp.Full("d")));
        File.AppendAllText(Temp.Full("b"), " edited");

        var last = Renamer.LoadLastBatch();
        var check = Renamer.CheckRevert();

        Assert.Equal(LastBatchState.Revertable, last.State);
        Assert.All(last.Renames, rename => Assert.Null(rename.Fingerprint));
        Assert.Empty(check.Skipped);
        Assert.False(check.AnyModified);
        Assert.True(check.AnyRevertable);
        Assert.Empty(Disk.FingerprintReads);

        var result = Renamer.Revert();

        Assert.Equal(RevertOutcome.Reverted, result.Outcome);
        Assert.Equal(["a", "c"], Temp.Listing());
        Assert.Equal("a edited", Temp.Content("a"));
    }

    [Fact]
    public void NewJournal_ReadByAnOldReader_IgnoresTheMembers()
    {
        RenameFiles(("a", "x"));

        // The entry contract as it was before NS-90: three required members, nothing else.
        OldLastBatch old;
        using (var stream = new MemoryStream(JournalBytes()))
        {
            old = (OldLastBatch)new DataContractJsonSerializer(typeof(OldLastBatch)).ReadObject(stream)!;
        }

        Assert.Equal(1, old.Version);
        var entry = Assert.Single(old.Renames!);
        Assert.Equal(("file", Temp.Full("a"), Temp.Full("x")), (entry.Kind, entry.Old, entry.New));
    }

    // AC 10

    [Fact]
    public void FolderEntry_JsonHasNoNulls()
    {
        Temp.Folder("D");

        Run(Changed(0, "D", "E", ItemKind.Folder));

        var text = Encoding.UTF8.GetString(JournalBytes());
        Assert.DoesNotContain("size", text);
        Assert.DoesNotContain("modified", text);
        Assert.DoesNotContain("null", text);
    }

    // AC 12

    [Fact]
    public void DiskFileSystem_TryGetFingerprint()
    {
        var file = Temp.File("f.txt", "12345");
        var folder = Temp.Folder("sub");
        var stamp = new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(6789);
        File.SetLastWriteTimeUtc(file, stamp);
        var before = Snapshot();
        var disk = new DiskFileSystem();

        var found = disk.TryGetFingerprint(file);

        Assert.NotNull(found);
        Assert.Equal(5, found!.Size);
        Assert.Equal(stamp.Ticks, found.ModifiedUtc.Ticks);
        Assert.Equal(DateTimeKind.Utc, found.ModifiedUtc.Kind);
        Assert.Null(disk.TryGetFingerprint(folder));
        Assert.Null(disk.TryGetFingerprint(Temp.Full("missing.txt")));
        Assert.Null(disk.TryGetFingerprint(Temp.Path + Path.DirectorySeparatorChar + "bad|name"));
        Assert.Equal(before, Snapshot());
    }

    /// <summary>Every entry of the temp folder with its size, last write time and attributes.</summary>
    private IReadOnlyList<string> Snapshot() =>
        Directory.GetFileSystemEntries(Temp.Path, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                var info = new FileInfo(path);
                var size = File.Exists(path) ? info.Length : -1;
                return $"{path}|{size}|{File.GetLastWriteTimeUtc(path).Ticks}|{File.GetAttributes(path)}";
            })
            .ToList();

    // The model

    [Fact]
    public void FileFingerprint_Equality()
    {
        var time = new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        var a = new FileFingerprint(10, time);

        Assert.True(a.Equals(new FileFingerprint(10, time)));
        Assert.True(a.Equals((object)new FileFingerprint(10, time)));
        Assert.Equal(a.GetHashCode(), new FileFingerprint(10, time).GetHashCode());
        Assert.False(a.Equals(new FileFingerprint(11, time)));
        Assert.False(a.Equals(new FileFingerprint(10, time.AddTicks(1))));
        Assert.False(a.Equals(new FileFingerprint(10, time.AddTicks(-1))));
        Assert.False(a.Equals(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileFingerprint(-1, time));
        Assert.Throws<ArgumentException>(() => new FileFingerprint(10, DateTime.SpecifyKind(time, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => new FileFingerprint(10, DateTime.SpecifyKind(time, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void CompletedRename_FolderWithFingerprint_Throws()
    {
        var fingerprint = new FileFingerprint(1, DateTime.UtcNow);

        Assert.Throws<ArgumentException>(() => new CompletedRename(Temp.Full("a"), Temp.Full("b"), ItemKind.Folder, fingerprint));
        Assert.Same(fingerprint, new CompletedRename(Temp.Full("a"), Temp.Full("b"), ItemKind.File, fingerprint).Fingerprint);
        Assert.Null(new CompletedRename(Temp.Full("a"), Temp.Full("b"), ItemKind.Folder).Fingerprint);
    }

    [Fact]
    public void CompletedRename_WithNewPath_KeepsTheRest()
    {
        var fingerprint = new FileFingerprint(1, DateTime.UtcNow);
        var entry = new CompletedRename(Temp.Full("a"), Temp.Full("b"), ItemKind.File, fingerprint);

        var moved = entry.WithNewPath(Temp.Full("c"));

        Assert.Equal((Temp.Full("a"), Temp.Full("c"), ItemKind.File), (moved.OldPath, moved.NewPath, moved.Kind));
        Assert.Same(fingerprint, moved.Fingerprint);
    }

    // The reader stays strict (NS-9): a half or invalid fingerprint makes the journal unreadable.

    [Fact]
    public void Journal_ValidFingerprint_Loads()
    {
        WriteJournal(Entry("file", Temp.Full("a"), Temp.Full("b"), ", 'size': 12, 'modified': '2026-10-25T08:00:00.1234567Z'"));

        var entry = Assert.Single(Renamer.LoadLastBatch().Renames);

        Assert.Equal(12, entry.Fingerprint!.Size);
        Assert.Equal(new DateTime(2026, 10, 25, 8, 0, 0, DateTimeKind.Utc).AddTicks(1234567), entry.Fingerprint.ModifiedUtc);
        Assert.Equal(DateTimeKind.Utc, entry.Fingerprint.ModifiedUtc.Kind);
    }

    [Theory]
    [InlineData("only size", "file", ", 'size': 12")]
    [InlineData("only modified", "file", ", 'modified': '2026-10-25T08:00:00.1234567Z'")]
    [InlineData("bad modified", "file", ", 'size': 12, 'modified': 'yesterday'")]
    [InlineData("modified with an offset", "file", ", 'size': 12, 'modified': '2026-10-25T08:00:00.1234567+02:00'")]
    [InlineData("modified without a zone", "file", ", 'size': 12, 'modified': '2026-10-25T08:00:00.1234567'")]
    [InlineData("negative size", "file", ", 'size': -1, 'modified': '2026-10-25T08:00:00.1234567Z'")]
    [InlineData("fingerprint on a folder", "folder", ", 'size': 0, 'modified': '2026-10-25T08:00:00.1234567Z'")]
    public void Journal_InvalidFingerprint_IsUnreadable(string row, string kind, string extra)
    {
        Assert.NotEmpty(row);
        WriteJournal(Entry(kind, Temp.Full("a"), Temp.Full("b"), extra));
        var bytes = JournalBytes();

        Assert.Equal(LastBatchState.Unreadable, Renamer.LoadLastBatch().State);
        Assert.Same(RevertCheck.Empty, Renamer.CheckRevert());
        Assert.Equal(RevertOutcome.NothingToRevert, Renamer.Revert().Outcome);
        Assert.Equal(bytes, JournalBytes());
    }

    [Fact]
    public void Journal_NullMembers_AreNoFingerprint()
    {
        WriteJournal(Entry("folder", Temp.Full("a"), Temp.Full("b"), ", 'size': null, 'modified': null"));

        var entry = Assert.Single(Renamer.LoadLastBatch().Renames);

        Assert.Null(entry.Fingerprint);
    }

    [DataContract]
    private sealed class OldLastBatch
    {
        [DataMember(Name = "version", IsRequired = true, Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "renames", IsRequired = true, Order = 6)]
        public OldEntry[]? Renames { get; set; }
    }

    [DataContract]
    private sealed class OldEntry
    {
        [DataMember(Name = "kind", IsRequired = true, Order = 1)]
        public string? Kind { get; set; }

        [DataMember(Name = "old", IsRequired = true, Order = 2)]
        public string? Old { get; set; }

        [DataMember(Name = "new", IsRequired = true, Order = 3)]
        public string? New { get; set; }
    }
}
