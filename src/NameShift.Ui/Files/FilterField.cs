namespace NameShift.Ui.Files;

/// <summary>The text boxes of the filter row, in the order <see cref="FilterTyping.Flush"/> applies them.</summary>
public enum FilterField
{
    /// <summary><see cref="FilterPanelModel.Mask"/>.</summary>
    Mask,

    /// <summary><see cref="FilterPanelModel.Pattern"/>.</summary>
    Pattern,

    /// <summary><see cref="FilterPanelModel.MinLengthText"/>.</summary>
    MinLength,

    /// <summary><see cref="FilterPanelModel.MaxLengthText"/>.</summary>
    MaxLength,
}
