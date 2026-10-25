using System;
using System.Collections.Generic;
using System.Drawing;
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
/// <remarks>
/// The wrap comes from <see cref="PanelFlow"/> only. A scrolling <c>FlowLayoutPanel</c> does not always wrap again when
/// its own vertical bar appears or goes (it can keep the old rows and add a horizontal bar), so the flow here never
/// scrolls: the stack sets its width (<see cref="PanelFlowResult.UsableWidth"/>: the area's width, or the width beside
/// the vertical bar when <see cref="PanelFlow.Settle"/> took its second pass) and its height (the rows' total), and a
/// plain outer panel does the scrolling. The flow is sized in <see cref="OnLayout"/>, before the outer panel is resized,
/// so the outer panel works out its bars from the flow's final size (sized after, it could keep a stale bar).
/// </remarks>
internal sealed class RulePanelStack : UserControl
{
    private readonly Panel _scroller;
    private readonly FlowLayoutPanel _flow;
    private readonly List<RulePanelFrame> _frames = new List<RulePanelFrame>();
    private bool _layoutPending;
    private bool _arranging;

    public RulePanelStack(RulePanelHostModel host, IReadOnlyList<RulePanelEntry> entries, PanelCollapseModel collapse)
    {
        if (entries.Any(e => !host.Panels.Contains(e.Model)))
        {
            throw new ArgumentException("An entry's model is not in the host.", nameof(entries));
        }

        _scroller = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
        _flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Margin = Padding.Empty,
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

        _scroller.Controls.Add(_flow);
        Controls.Add(_scroller);
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

        ArrangeFlow();
    }

    /// <summary>Sizes the flow for the new size before the outer panel is resized and works out its scroll bars, so
    /// they are computed from the flow's final size.</summary>
    protected override void OnLayout(LayoutEventArgs e)
    {
        ArrangeFlow();
        base.OnLayout(e);
    }

    /// <summary>The stack's height, in device pixels, for the wrapped panels to show without clipping, at most
    /// <paramref name="cap"/> (above it the panels scroll). A horizontal bar, shown when one panel is wider than the
    /// area, counts within the cap. The caller applies its own minimum.</summary>
    internal int PreferredAreaHeight(int cap)
    {
        // The stack's own padding is outside the scrolling area: take it off the cap, then add it back.
        int padding = Padding.Vertical;
        Arrangement a = Arrange(DisplayRectangle.Width, Math.Max(0, cap - padding));
        return Math.Min(cap, a.Flow.ContentHeight + a.HorizontalBar + padding);
    }

    /// <summary>How the panels wrap in a scrolling area of this outer size (scroll bars included), from
    /// <see cref="PanelFlow.Settle"/>: the rows, the flow's width, and the horizontal bar's height when one panel is wider
    /// than the area (0 otherwise).</summary>
    private Arrangement Arrange(int outerWidth, int outerHeight)
    {
        var boxes = _frames.Select(f => f.Box).ToList();
        int margin = _frames.Count > 0 ? _frames[0].Margin.Left : 0; // every frame has the same margin
        int widest = boxes.Count > 0 ? boxes.Max(b => b.Width + 2 * margin) : 0;
        int verticalBar = SystemInformation.VerticalScrollBarWidth;
        int horizontalBar = widest > outerWidth ? SystemInformation.HorizontalScrollBarHeight : 0;
        int cap = Math.Max(0, outerHeight - horizontalBar);
        PanelFlowResult flow = PanelFlow.Settle(boxes, margin, Math.Max(0, outerWidth), verticalBar, 0, cap);
        int usable = flow.UsableWidth;
        if (widest > usable)
        {
            horizontalBar = SystemInformation.HorizontalScrollBarHeight;
        }

        return new Arrangement(flow, Math.Max(usable, widest), horizontalBar);
    }

    /// <summary>Gives the flow the width and height of <see cref="Arrange"/> at the area's current size, so it wraps as
    /// <see cref="PanelFlow"/> says. Only its size is set: the outer panel moves it when scrolled.</summary>
    private void ArrangeFlow()
    {
        if (_arranging)
        {
            return;
        }

        _arranging = true;
        try
        {
            Rectangle area = DisplayRectangle; // where the outer panel fills, scroll bars included
            Arrangement a = Arrange(area.Width, area.Height);
            var size = new Size(a.FlowWidth, a.Flow.ContentHeight);
            if (_flow.Size != size)
            {
                _flow.Size = size;
            }
        }
        finally
        {
            _arranging = false;
        }
    }

    private void OnBoxChanged(object? sender, EventArgs e)
    {
        if (_layoutPending || !IsHandleCreated || IsDisposed)
        {
            return; // before the handle, StartCollapse arranges and the window fits the area at its Load
        }

        // After the frames' own layout has run: the flow is arranged again, then the window refits the area.
        _layoutPending = true;
        BeginInvoke(new Action(() =>
        {
            _layoutPending = false;
            if (!IsDisposed)
            {
                ArrangeFlow();
                LayoutNeeded?.Invoke(this, EventArgs.Empty);
            }
        }));
    }

    private readonly struct Arrangement
    {
        public Arrangement(PanelFlowResult flow, int flowWidth, int horizontalBar)
        {
            Flow = flow;
            FlowWidth = flowWidth;
            HorizontalBar = horizontalBar;
        }

        public PanelFlowResult Flow { get; }

        public int FlowWidth { get; }

        public int HorizontalBar { get; }
    }
}
