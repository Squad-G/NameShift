using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NameShift.Core;

namespace NameShift.Ui.Preview;

/// <summary>The words for conflicts: the Status cell and the summary line. The one place the messages are built.</summary>
public static class ConflictText
{
    /// <summary>
    /// The message of one conflict, from Strings.resx: the problem, then what to do. The detail is escaped; the limits
    /// are read from the checks and written in the invariant culture.
    /// </summary>
    public static string Message(Conflict conflict)
    {
        if (conflict == null)
        {
            throw new ArgumentNullException(nameof(conflict));
        }

        CultureInfo culture = CultureInfo.CurrentCulture;
        switch (conflict.Code)
        {
            case ConflictCode.InvalidCharacter:
                string characters = conflict.Detail ?? "";
                return string.Format(
                    culture,
                    characters.Length == 1 ? Strings.ConflictInvalidCharacterOne : Strings.ConflictInvalidCharacter,
                    EscapeEach(characters));
            case ConflictCode.ReservedName:
                string name = conflict.Detail ?? "";
                return name.Length == 0
                    ? Strings.ConflictReservedNameNoDetail
                    : string.Format(culture, Strings.ConflictReservedName, Escape(name));
            case ConflictCode.TrailingDotOrSpace:
                return Strings.ConflictTrailingDotOrSpace;
            case ConflictCode.EmptyName:
                return Strings.ConflictEmptyName;
            case ConflictCode.NameTooLong:
                return string.Format(
                    culture,
                    Strings.ConflictNameTooLong,
                    InvalidNameCheck.MaxNameLength.ToString(CultureInfo.InvariantCulture));
            case ConflictCode.DuplicateTarget:
                return Strings.ConflictDuplicateTarget;
            case ConflictCode.TargetExists:
                return Strings.ConflictTargetExists;
            case ConflictCode.PathTooLong:
                return string.Format(
                    culture,
                    Strings.ConflictPathTooLong,
                    PathLengthCheck.MaxPathLength.ToString(CultureInfo.InvariantCulture));
            default:
                throw new ArgumentOutOfRangeException(nameof(conflict), conflict.Code, "Unknown conflict code.");
        }
    }

    /// <summary>The Status cell: each conflict's message in report order, joined by Strings.ConflictSeparator; "" when none.</summary>
    public static string For(IReadOnlyList<Conflict> conflicts)
    {
        if (conflicts == null)
        {
            throw new ArgumentNullException(nameof(conflicts));
        }

        return string.Join(Strings.ConflictSeparator, conflicts.Select(Message));
    }

    /// <summary>"" when 0, else "1 conflict" or "N conflicts" (the number in the current culture).</summary>
    public static string Summary(int conflictCount)
    {
        if (conflictCount <= 0)
        {
            return "";
        }

        return conflictCount == 1
            ? Strings.ConflictSummaryOne
            : string.Format(
                CultureInfo.CurrentCulture,
                Strings.ConflictSummaryMany,
                conflictCount.ToString("N0", CultureInfo.CurrentCulture));
    }

    /// <summary>Every control character (C0, DEL, C1) becomes "U+XXXX" (4 upper-case hex digits); other characters stay.</summary>
    internal static string Escape(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (char.IsControl(c))
            {
                builder.Append("U+").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>Each character escaped on its own, separated by a space.</summary>
    private static string EscapeEach(string? detail) =>
        detail == null ? "" : string.Join(" ", detail.Select(c => Escape(c.ToString())));
}
