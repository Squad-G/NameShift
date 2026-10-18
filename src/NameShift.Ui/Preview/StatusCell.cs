namespace NameShift.Ui.Preview;

/// <summary>The text of one Status cell and whether the row has a conflict. No UI types: the window decides colour.</summary>
public readonly struct StatusCell
{
    public StatusCell(string text, bool isConflict)
    {
        Text = text;
        IsConflict = isConflict;
    }

    /// <summary>The empty cell: no conflict.</summary>
    public static StatusCell None => new StatusCell("", false);

    public string Text { get; }

    /// <summary>True exactly when the row has at least one conflict.</summary>
    public bool IsConflict { get; }
}
