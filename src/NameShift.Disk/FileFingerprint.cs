using System;

namespace NameShift.Disk;

/// <summary>A file's size and last write time, read right after a batch, so that Revert can tell a file still as the
/// batch left it from one changed afterwards. Both are compared exactly (bytes, and 100 ns ticks in UTC).</summary>
public sealed class FileFingerprint : IEquatable<FileFingerprint>
{
    internal FileFingerprint(long size, DateTime modifiedUtc)
    {
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        if (modifiedUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("The time must be UTC.", nameof(modifiedUtc));
        }

        Size = size;
        ModifiedUtc = modifiedUtc;
    }

    /// <summary>The file's size in bytes.</summary>
    public long Size { get; }

    /// <summary>The file's last write time (Kind Utc).</summary>
    public DateTime ModifiedUtc { get; }

    /// <summary>True when both the size and the time's ticks are the same.</summary>
    public bool Equals(FileFingerprint? other) =>
        other != null && Size == other.Size && ModifiedUtc.Ticks == other.ModifiedUtc.Ticks;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FileFingerprint);

    /// <inheritdoc />
    public override int GetHashCode() => (Size.GetHashCode() * 397) ^ ModifiedUtc.Ticks.GetHashCode();
}
