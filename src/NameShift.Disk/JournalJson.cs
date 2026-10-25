using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using NameShift.Core;

namespace NameShift.Disk;

/// <summary>The journal's JSON format, version 1, through the in-box DataContractJsonSerializer: UTF-8 without BOM,
/// indented. Times are "o" strings in UTC; kinds are "file" / "folder". A file entry of last-batch.json may hold its
/// fingerprint as "size" (bytes) and "modified" (the last write time, an "o" string in UTC, 7 fractional digits so the
/// ticks round-trip): both or neither, written only when set, never on a folder (optional members, still version 1).
/// Versioning: adding an optional member later (IsRequired = false) keeps version 1, since readers ignore unknown
/// members; any breaking change bumps the version, and a reader treats any version but 1 as unreadable.</summary>
internal static class JournalJson
{
    /// <summary>The only version this code reads and writes.</summary>
    public const int Version = 1;

    /// <summary>The pending operation of a batch.</summary>
    public const string RenameOperation = "rename";

    /// <summary>The pending operation of a revert.</summary>
    public const string RevertOperation = "revert";

    private const string StateDone = "done";
    private const string StateReverted = "reverted";
    private const string KindFile = "file";
    private const string KindFolder = "folder";

    /// <summary>last-batch.json: "done" with the entries, or "reverted" with empty lists. Folders are the distinct
    /// parents of the old paths, first-seen order.</summary>
    public static byte[] SerializeLastBatch(string id, bool isReverted, DateTimeOffset time, IReadOnlyList<CompletedRename> renames)
    {
        var entries = isReverted ? Array.Empty<CompletedRename>() : renames.ToArray();
        return Serialize(new LastBatchDto
        {
            Version = Version,
            Id = id,
            State = isReverted ? StateReverted : StateDone,
            Time = FormatTime(time),
            Folders = entries
                .Select(entry => Path.GetDirectoryName(entry.OldPath)!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Renames = entries
                .Select(entry => Entry(entry.Kind, entry.OldPath, entry.NewPath, entry.Fingerprint))
                .ToArray(),
        });
    }

    /// <summary>pending.json: each rename as old = FolderPath\OldName and new = FolderPath\NewName, as given (for a
    /// batch: pre-batch coordinates, parent renames not applied; for a revert: the inverses about to run). Only the id
    /// is ever read back; the rest is for a person who finds the file after a crash.</summary>
    public static byte[] SerializePending(string id, string operation, DateTimeOffset time, IEnumerable<PlannedRename> renames) =>
        Serialize(new PendingDto
        {
            Version = Version,
            Id = id,
            Operation = operation,
            Time = FormatTime(time),
            Renames = renames
                .Select(rename => Entry(
                    rename.Kind, Path.Combine(rename.FolderPath, rename.OldName), Path.Combine(rename.FolderPath, rename.NewName)))
                .ToArray(),
        });

    /// <summary>Parses and validates last-batch.json; throws on anything invalid (the caller treats every exception as
    /// unreadable).</summary>
    public static JournalRecord ParseLastBatch(byte[] content)
    {
        var dto = Deserialize<LastBatchDto>(content);
        Require(dto.Version == Version && !string.IsNullOrEmpty(dto.Id), "Unknown version or no id.");
        Require(dto.State == StateDone || dto.State == StateReverted, "Unknown state.");
        Require(dto.Time != null && dto.Folders != null && dto.Renames != null, "Missing time or lists.");
        Require(dto.Folders!.All(folder => folder != null), "A null folder.");
        var time = DateTimeOffset.ParseExact(dto.Time!, "o", CultureInfo.InvariantCulture, DateTimeStyles.None);

        var renames = dto.Renames!.Select(ToCompletedRename).ToArray();
        var newPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rename in renames)
        {
            var inverse = rename.Inverse();
            Require(newPaths.Add(FolderKey.NormalizePath(rename.NewPath)), "Two entries share a new path.");
            Require(targets.Add(FolderKey.NormalizePath(Path.Combine(inverse.FolderPath, inverse.NewName))), "Two entries share an old path.");
        }

        var isReverted = dto.State == StateReverted;
        Require(isReverted ? renames.Length == 0 && dto.Folders!.Length == 0 : renames.Length > 0, "Entries do not match the state.");
        return new JournalRecord(dto.Id!, isReverted, time, dto.Folders!, renames);
    }

    /// <summary>The id of a pending.json; throws when the file is invalid.</summary>
    public static string ParsePendingId(byte[] content)
    {
        var dto = Deserialize<PendingIdDto>(content);
        Require(dto.Version == Version && !string.IsNullOrEmpty(dto.Id), "Unknown version or no id.");
        return dto.Id!;
    }

    private static CompletedRename ToCompletedRename(EntryDto? entry)
    {
        Require(entry != null, "A null entry.");
        Require(entry!.Kind == KindFile || entry.Kind == KindFolder, "Unknown kind.");
        Require(entry.Old != null && entry.New != null, "A null path.");
        Require(Path.IsPathRooted(entry.Old) && Path.IsPathRooted(entry.New), "A relative path.");
        var kind = entry.Kind == KindFile ? ItemKind.File : ItemKind.Folder;
        return new CompletedRename(entry.Old!, entry.New!, kind, ToFingerprint(entry, kind));
    }

    /// <summary>No members: no fingerprint (a journal written before them, a folder, or a file that could not be read).
    /// Only one of the two, an invalid value or a fingerprint on a folder throws, as every other invalid entry.</summary>
    private static FileFingerprint? ToFingerprint(EntryDto entry, ItemKind kind)
    {
        if (entry.Size == null && entry.Modified == null)
        {
            return null;
        }

        Require(entry.Size != null && entry.Modified != null, "Half a fingerprint.");
        Require(kind == ItemKind.File, "A fingerprint on a folder.");
        Require(entry.Size >= 0, "A negative size.");
        Require(
            DateTime.TryParseExact(entry.Modified, "o", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var modified)
                && modified.Kind == DateTimeKind.Utc,
            "A bad modified time.");
        return new FileFingerprint(entry.Size!.Value, modified);
    }

    private static EntryDto Entry(ItemKind kind, string oldPath, string newPath, FileFingerprint? fingerprint = null) =>
        new()
        {
            Kind = kind == ItemKind.File ? KindFile : KindFolder,
            Old = oldPath,
            New = newPath,
            Size = fingerprint?.Size,
            Modified = fingerprint?.ModifiedUtc.ToString("o", CultureInfo.InvariantCulture),
        };

    private static string FormatTime(DateTimeOffset time) =>
        time.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new SerializationException(message);
        }
    }

    private static byte[] Serialize<T>(T value)
    {
        using var stream = new MemoryStream();
        using (var writer = JsonReaderWriterFactory.CreateJsonWriter(
            stream, new UTF8Encoding(false), ownsStream: false, indent: true, indentChars: "  "))
        {
            new DataContractJsonSerializer(typeof(T)).WriteObject(writer, value);
            writer.Flush();
        }

        return stream.ToArray();
    }

    /// <summary>Reads through <see cref="JsonReaderQuotas"/>: a file nested deeper than its MaxDepth throws at once
    /// (no size limit: a big batch is a real journal).</summary>
    private static T Deserialize<T>(byte[] content)
    {
        using XmlDictionaryReader reader = JsonReaderWriterFactory.CreateJsonReader(content, JsonReaderQuotas.Create());
        return (T?)new DataContractJsonSerializer(typeof(T)).ReadObject(reader)
            ?? throw new SerializationException("The file holds null.");
    }

    [DataContract]
    internal sealed class LastBatchDto
    {
        [DataMember(Name = "version", IsRequired = true, Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "id", IsRequired = true, Order = 2)]
        public string? Id { get; set; }

        [DataMember(Name = "state", IsRequired = true, Order = 3)]
        public string? State { get; set; }

        [DataMember(Name = "time", IsRequired = true, Order = 4)]
        public string? Time { get; set; }

        [DataMember(Name = "folders", IsRequired = true, Order = 5)]
        public string?[]? Folders { get; set; }

        [DataMember(Name = "renames", IsRequired = true, Order = 6)]
        public EntryDto?[]? Renames { get; set; }
    }

    [DataContract]
    internal sealed class PendingDto
    {
        [DataMember(Name = "version", IsRequired = true, Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "id", IsRequired = true, Order = 2)]
        public string? Id { get; set; }

        [DataMember(Name = "operation", IsRequired = true, Order = 3)]
        public string? Operation { get; set; }

        [DataMember(Name = "time", IsRequired = true, Order = 4)]
        public string? Time { get; set; }

        [DataMember(Name = "renames", IsRequired = true, Order = 5)]
        public EntryDto?[]? Renames { get; set; }
    }

    /// <summary>The part of pending.json that is read back: the rest is ignored as unknown members.</summary>
    [DataContract]
    internal sealed class PendingIdDto
    {
        [DataMember(Name = "version", IsRequired = true, Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "id", IsRequired = true, Order = 2)]
        public string? Id { get; set; }
    }

    [DataContract]
    internal sealed class EntryDto
    {
        [DataMember(Name = "kind", IsRequired = true, Order = 1)]
        public string? Kind { get; set; }

        [DataMember(Name = "old", IsRequired = true, Order = 2)]
        public string? Old { get; set; }

        [DataMember(Name = "new", IsRequired = true, Order = 3)]
        public string? New { get; set; }

        /// <summary>The file's size in bytes after the batch; omitted when there is no fingerprint.</summary>
        [DataMember(Name = "size", EmitDefaultValue = false, Order = 4)]
        public long? Size { get; set; }

        /// <summary>The file's last write time after the batch, "o" in UTC; omitted when there is no fingerprint.</summary>
        [DataMember(Name = "modified", EmitDefaultValue = false, Order = 5)]
        public string? Modified { get; set; }
    }
}
