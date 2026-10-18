using System;
using System.Globalization;
using System.Linq;
using NameShift.Core;

namespace NameShift.Ui.Preview;

/// <summary>The New Name cell for one pipeline result.</summary>
public static class PreviewText
{
    public static PreviewCell For(RenameResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        switch (result.Status)
        {
            case RenameStatus.Changed:
                return new PreviewCell(result.NewName, PreviewCellKind.Changed);
            case RenameStatus.Unchanged:
                return new PreviewCell(result.NewName, PreviewCellKind.Unchanged);
            case RenameStatus.Error:
                string text = string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.PreviewError,
                    OneLine(result.Error!.Message));
                return new PreviewCell(text, PreviewCellKind.Error);
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown rename status.");
        }
    }

    /// <summary>The cell is one line: every control character (newline, tab...) becomes a space.</summary>
    private static string OneLine(string message) =>
        new string(message.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
}
