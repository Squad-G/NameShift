using System;

namespace NameShift.Ui.Files;

/// <summary>A sort of the file list: a column and a direction. Immutable.</summary>
public sealed class FileListSort : IEquatable<FileListSort>
{
    /// <summary>Name, ascending: the order of a freshly loaded folder.</summary>
    public static readonly FileListSort Default = new FileListSort(FileListColumn.Name, false);

    /// <summary>Creates a sort.</summary>
    /// <exception cref="ArgumentOutOfRangeException">When the column is not a defined value.</exception>
    public FileListSort(FileListColumn column, bool descending)
    {
        if (!Enum.IsDefined(typeof(FileListColumn), column))
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, "The column is not sortable.");
        }

        Column = column;
        Descending = descending;
    }

    /// <summary>The column sorted by.</summary>
    public FileListColumn Column { get; }

    /// <summary>True for the reverse order of the column (ties still ascending by name).</summary>
    public bool Descending { get; }

    /// <summary>The same column in the other direction.</summary>
    public FileListSort Toggled() => new FileListSort(Column, !Descending);

    /// <inheritdoc />
    public bool Equals(FileListSort? other) => other != null && other.Column == Column && other.Descending == Descending;

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as FileListSort);

    /// <inheritdoc />
    public override int GetHashCode() => ((int)Column * 2) + (Descending ? 1 : 0);

    /// <inheritdoc />
    public override string ToString() => Column + (Descending ? " descending" : " ascending");
}
