using System;
using NameShift.Ui.Rules;

namespace NameShift.Ui.Files;

/// <summary>The texts of the filter row that depend on the model (NS-70): the More toggle's mark and description, and
/// the accessible names of the text boxes.</summary>
public static class FilterRowText
{
    /// <summary>True when a value of the More line (regex, ignore case, minimum or maximum length) is not at its
    /// default. The texts are compared as stored, as <see cref="FilterPanelModel.IsDefault"/> does, so an invalid
    /// length still counts.</summary>
    public static bool HasHiddenValues(FilterPanelModel filters) =>
        filters.Pattern.Length > 0
        || filters.IgnoreCase
        || filters.MinLengthText.Length > 0
        || filters.MaxLengthText.Length > 0;

    /// <summary>"More *" when the More line is hidden and holds a value that is not at default, else "More" (both with
    /// the same Alt key).</summary>
    public static string MoreText(FilterPanelModel filters, bool lineShown) =>
        IsMarked(filters, lineShown) ? Strings.FilterMoreMarked : Strings.FilterMore;

    /// <summary>The More toggle's accessible description: "Regex or length filter active" in the same case as the mark
    /// (so the star is not the only cue), else "".</summary>
    public static string MoreDescription(FilterPanelModel filters, bool lineShown) =>
        IsMarked(filters, lineShown) ? Strings.FilterMoreActive : "";

    /// <summary>The accessible name of a text box from its label: the label without its Alt key mark and without a
    /// trailing colon ("Mas&amp;k:" gives "Mask").</summary>
    public static string FieldName(string label)
    {
        string plain = RulePanelText.PlainTitle(label);
        return plain.EndsWith(":", StringComparison.Ordinal) ? plain.Substring(0, plain.Length - 1) : plain;
    }

    private static bool IsMarked(FilterPanelModel filters, bool lineShown) => !lineShown && HasHiddenValues(filters);
}
