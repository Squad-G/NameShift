namespace NameShift.Ui.Preview;

/// <summary>The text and style kind of one New Name cell. No UI types: the window decides colour and font.</summary>
public readonly struct PreviewCell
{
    public PreviewCell(string text, PreviewCellKind kind)
    {
        Text = text;
        Kind = kind;
    }

    /// <summary>The empty cell.</summary>
    public static PreviewCell None => new PreviewCell("", PreviewCellKind.None);

    public string Text { get; }

    public PreviewCellKind Kind { get; }
}
