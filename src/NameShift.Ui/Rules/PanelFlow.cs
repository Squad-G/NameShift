using System;
using System.Collections.Generic;

namespace NameShift.Ui.Rules;

/// <summary>
/// The height of the rule panel area (NS-83): the panels wrapped the way a left-to-right, wrapping
/// <c>FlowLayoutPanel</c> lays them out. Each panel takes its size plus its margin on every side (margins do not
/// collapse); a panel that does not fit in what is left of the row starts a new one, unless the row is empty (a panel
/// wider than the area sits alone on its row); a row is as tall as its tallest panel plus both margins.
/// </summary>
public static class PanelFlow
{
    /// <summary>Wraps the panels at one width.</summary>
    /// <param name="panels">The panels in pipeline order.</param>
    /// <param name="margin">The margin on each side of every panel.</param>
    /// <param name="availableWidth">The usable width: the area's client width, minus a shown vertical scroll bar.</param>
    /// <param name="minimum">The lowest height (the action buttons).</param>
    /// <param name="cap">The highest height; above it the area scrolls.</param>
    /// <exception cref="ArgumentNullException">The panels are null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A size is negative, or the minimum is above the cap.</exception>
    public static PanelFlowResult Measure(IReadOnlyList<PanelBox> panels, int margin, int availableWidth, int minimum, int cap)
    {
        CheckCommon(panels, margin, minimum, cap);
        if (availableWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableWidth));
        }

        var rows = new List<IReadOnlyList<int>>();
        List<int>? row = null;
        int used = 0;
        int rowHeight = 0;
        int content = 0;
        for (int i = 0; i < panels.Count; i++)
        {
            int width = panels[i].Width + 2 * margin;
            int height = panels[i].Height + 2 * margin;
            if (row != null && used + width > availableWidth)
            {
                rows.Add(row);
                content += rowHeight;
                row = null;
            }

            if (row == null)
            {
                row = new List<int>();
                used = 0;
                rowHeight = 0;
            }

            row.Add(i);
            used += width;
            rowHeight = Math.Max(rowHeight, height);
        }

        if (row != null)
        {
            rows.Add(row);
            content += rowHeight;
        }

        return new PanelFlowResult(rows, content, Math.Min(cap, Math.Max(minimum, content)), content > cap);
    }

    /// <summary>
    /// Settles the scroll bar and wrap loop: wraps at the full client width; when that scrolls, wraps again at the
    /// width left beside the vertical scroll bar, and that answer is final (a narrower width only adds rows, so the
    /// content stays above the cap and the bar stays).
    /// </summary>
    /// <param name="clientWidth">The area's client width with no vertical scroll bar.</param>
    /// <param name="scrollBarWidth">The vertical scroll bar's width.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is negative, or the minimum is above the cap.</exception>
    public static PanelFlowResult Settle(
        IReadOnlyList<PanelBox> panels, int margin, int clientWidth, int scrollBarWidth, int minimum, int cap)
    {
        CheckCommon(panels, margin, minimum, cap);
        if (clientWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(clientWidth));
        }

        if (scrollBarWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scrollBarWidth));
        }

        PanelFlowResult first = Measure(panels, margin, clientWidth, minimum, cap);
        if (!first.NeedsScroll)
        {
            return first;
        }

        return Measure(panels, margin, Math.Max(0, clientWidth - scrollBarWidth), minimum, cap);
    }

    private static void CheckCommon(IReadOnlyList<PanelBox> panels, int margin, int minimum, int cap)
    {
        if (panels == null)
        {
            throw new ArgumentNullException(nameof(panels));
        }

        if (margin < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(margin));
        }

        if (minimum < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum));
        }

        if (cap < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(cap), "The cap is below the minimum.");
        }
    }
}
