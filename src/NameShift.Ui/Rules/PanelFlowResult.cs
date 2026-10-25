using System.Collections.Generic;

namespace NameShift.Ui.Rules;

/// <summary>How <see cref="PanelFlow"/> wrapped the panels, and the height the panel area takes.</summary>
public sealed class PanelFlowResult
{
    internal PanelFlowResult(IReadOnlyList<IReadOnlyList<int>> rows, int contentHeight, int height, bool needsScroll)
    {
        Rows = rows;
        ContentHeight = contentHeight;
        Height = height;
        NeedsScroll = needsScroll;
    }

    /// <summary>The rows, top to bottom, each with the indexes of its panels, left to right.</summary>
    public IReadOnlyList<IReadOnlyList<int>> Rows { get; }

    /// <summary>The sum of the row heights, margins included.</summary>
    public int ContentHeight { get; }

    /// <summary>The content height, raised to the minimum and lowered to the cap.</summary>
    public int Height { get; }

    /// <summary>True when the content is taller than the cap: the area scrolls vertically.</summary>
    public bool NeedsScroll { get; }
}
