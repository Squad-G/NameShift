namespace NameShift.Ui.Preview;

/// <summary>How a New Name cell is shown; the window maps it to a colour and a font.</summary>
public enum PreviewCellKind
{
    /// <summary>Nothing to show.</summary>
    None,

    /// <summary>The name is the original one (muted).</summary>
    Unchanged,

    /// <summary>The name changes (green and bold).</summary>
    Changed,

    /// <summary>A rule failed; the text is the error.</summary>
    Error,
}
