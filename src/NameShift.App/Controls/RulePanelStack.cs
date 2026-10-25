using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>
/// The rule panels as a wrapping, vertically scrolling stack of frames in pipeline order. Each frame collapses to its
/// title line (NS-83); the window sizes the area from <see cref="PreferredAreaHeight"/> when <see cref="LayoutNeeded"/>
/// is raised.
/// </summary>
internal sealed class RulePanelStack : UserControl
{
    private readonly FlowLayoutPanel _flow;
    private readonly List<RulePanelFrame> _frames = new List<RulePanelFrame>();
    private bool _layoutPending;

    public RulePanelStack(RulePanelHostModel host, IReadOnlyList<RulePanelEntry> entries, PanelCollapseModel collapse)
    {
        if (entries.Any(e => !host.Panels.Contains(e.Model)))
        {
            throw new ArgumentException("An entry's model is not in the host.", nameof(entries));
        }

        _flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            AccessibleName = Strings.RulePanelsName,
        };

        for (int i = 0; i < host.Panels.Count; i++)
        {
            RulePanelEntry? entry = entries.FirstOrDefault(e => ReferenceEquals(e.Model, host.Panels[i]));
            if (entry == null)
            {
                throw new ArgumentException("A host panel has no entry.", nameof(entries));
            }

            var frame = new RulePanelFrame(entry.Model, entry.Content, collapse) { TabIndex = i };
            frame.BoxChanged += OnBoxChanged;
            _frames.Add(frame);
            _flow.Controls.Add(frame);
        }

        Controls.Add(_flow);
    }

    /// <summary>Raised once per batch of frame changes (a preset that opens five panels gives one), after the
    /// changes, when the area's height may have to follow.</summary>
    internal event EventHandler? LayoutNeeded;

    /// <summary>Hides the content of the panels that start collapsed. Called once, when the window has its font and
    /// scale, so each frame keeps its real open width.</summary>
    internal void StartCollapse()
    {
        foreach (RulePanelFrame frame in _frames)
        {
            frame.StartCollapse();
        }
    }

    /// <summary>The height the stack needs so the wrapped panels show without clipping, from <see cref="PanelFlow"/>:
    /// never below <paramref name="minimum"/>, never above <paramref name="cap"/> (it scrolls there). A horizontal
    /// scroll bar, shown when a panel is wider than the area, counts within the cap.</summary>
    internal int PreferredAreaHeight(int minimum, int cap)
    {
        var boxes = _frames.Select(f => f.Box).ToList();
        int margin = _frames.Count > 0 ? _frames[0].Margin.Left : 0; // every frame has the same margin
        int scrollBar = SystemInformation.VerticalScrollBarWidth;
        int clientWidth = _flow.ClientSize.Width + (_flow.VerticalScroll.Visible ? scrollBar : 0);
        int extra = Padding.Vertical + _flow.Padding.Vertical;
        int innerMinimum = Math.Max(0, minimum - extra);
        int innerCap = Math.Max(innerMinimum, cap - extra);
        PanelFlowResult result = PanelFlow.Settle(boxes, margin, clientWidth, scrollBar, innerMinimum, innerCap);
        int usableWidth = result.NeedsScroll ? clientWidth - scrollBar : clientWidth;
        bool horizontal = boxes.Any(b => b.Width + 2 * margin > usableWidth);
        int height = result.Height + extra + (horizontal ? SystemInformation.HorizontalScrollBarHeight : 0);
        return Math.Min(cap, Math.Max(minimum, height));
    }

    private void OnBoxChanged(object? sender, EventArgs e)
    {
        if (_layoutPending || !IsHandleCreated || IsDisposed)
        {
            return; // before the handle the window fits the area at its Load
        }

        _layoutPending = true;
        BeginInvoke(new Action(() =>
        {
            _layoutPending = false;
            if (!IsDisposed)
            {
                LayoutNeeded?.Invoke(this, EventArgs.Empty);
            }
        }));
    }
}
