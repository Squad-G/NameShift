using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using NameShift.App.Style;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>
/// The frame around one rule panel: stage number and title as the enable checkbox, a Reset button and an arrow that
/// collapses the panel to its title line (NS-83), the content below. An active panel (enabled with a non-default setting)
/// gets a thicker accent border and a bold title: not colour alone, and also when collapsed. A collapsed frame keeps the
/// width it had open, so the panels to its right never move sideways.
/// </summary>
internal sealed class RulePanelFrame : UserControl
{
    private static readonly Size MinimumLogicalSize = new Size(240, 0);

    private readonly IRulePanelModel _model;
    private readonly Control _content;
    private readonly PanelCollapseModel _collapse;
    private readonly CheckBox _check = new CheckBox();
    private readonly Button _reset = new Button();
    private readonly Button _arrow = new Button();
    private Font _boldFont;
    private Color _accent;
    private bool _started;
    private bool _shownCollapsed;
    private int _openLogicalWidth;

    public RulePanelFrame(IRulePanelModel model, Control content, PanelCollapseModel collapse)
    {
        _model = model;
        _content = content;
        _collapse = collapse;
        _boldFont = new Font(Font, FontStyle.Bold);
        _accent = Palette.Accent;
        ResizeRedraw = true;
        DoubleBuffered = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ApplyLogicalMetrics();
        BuildLayout();
        Sync();

        _check.CheckedChanged += (_, _) => _model.Enabled = _check.Checked;
        _reset.Click += (_, _) => _model.Reset();
        _arrow.Click += (_, _) => _collapse.Toggle(_model);
        _model.Changed += OnModelChanged;
        _collapse.Changed += OnCollapseChanged;
    }

    /// <summary>Raised when the frame's size for the panel area's height may have changed: it collapsed or opened, or
    /// its size changed (an open panel whose content grows).</summary>
    internal event EventHandler? BoxChanged;

    /// <summary>The frame's size for <see cref="PanelFlow"/>, in device pixels, without its margin. Measured, so it is
    /// right even before the parent has laid the frame out again.</summary>
    internal PanelBox Box
    {
        get
        {
            int width = Math.Max(MinimumSize.Width, GetPreferredSize(Size.Empty).Width);
            int title = 0;
            foreach (Control control in new Control[] { _check, _reset, _arrow })
            {
                title = Math.Max(title, control.PreferredSize.Height + control.Margin.Vertical);
            }

            int collapsedHeight = Padding.Vertical + title;
            int contentWidth = Math.Max(0, width - Padding.Horizontal - _content.Margin.Horizontal);
            int openHeight = collapsedHeight
                + _content.GetPreferredSize(new Size(contentWidth, 0)).Height
                + _content.Margin.Vertical;
            return new PanelBox(width, openHeight, collapsedHeight, _collapse.IsCollapsed(_model));
        }
    }

    /// <summary>Applies the collapse state for the first time. Called once the window has its font and scale (its
    /// Load), so a panel that starts collapsed keeps its real open width; until then only the arrow follows the state.</summary>
    internal void StartCollapse()
    {
        _started = true;
        ApplyCollapsed();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        bool active = RulePanelHostModel.IsActive(_model);
        int width = active ? Math.Max(2, LogicalToDeviceUnits(2)) : Math.Max(1, LogicalToDeviceUnits(1));
        Color color = active ? _accent : SystemColors.ControlDark;
        Rectangle area = Rectangle.Inflate(ClientRectangle, -LogicalToDeviceUnits(1), -LogicalToDeviceUnits(1));
        using (var pen = new Pen(color, width) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset })
        {
            e.Graphics.DrawRectangle(pen, area.X, area.Y, area.Width - 1, area.Height - 1);
        }
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        BoxChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (!_shownCollapsed)
        {
            MinimumSize = LogicalToDeviceUnits(MinimumLogicalSize); // the open width is measured again on the next collapse
        }

        RefreshBoldFont();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyLogicalMetrics();
        _accent = Palette.Accent;
        RefreshBoldFont();
    }

    protected override void OnSystemColorsChanged(EventArgs e)
    {
        base.OnSystemColorsChanged(e);
        _accent = Palette.Accent;
        Sync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _model.Changed -= OnModelChanged;
            _collapse.Changed -= OnCollapseChanged;
            _boldFont.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Sizes from logical pixels. While collapsed the frame keeps the width it had open (stored in logical
    /// pixels); while open the minimum goes back to the logical one, so the open width is measured again.</summary>
    private void ApplyLogicalMetrics()
    {
        MinimumSize = _shownCollapsed
            ? new Size(LogicalToDeviceUnits(_openLogicalWidth), 0)
            : LogicalToDeviceUnits(MinimumLogicalSize);
        Margin = new Padding(LogicalToDeviceUnits(Spacing.Small));
        Padding = new Padding(LogicalToDeviceUnits(Spacing.Small));
    }

    private void BuildLayout()
    {
        _check.Text = RulePanelText.Heading(_model.Stage, _model.Title);
        _check.AutoSize = true;
        _check.Anchor = AnchorStyles.Left;
        _check.TabIndex = 0;

        _reset.Text = Strings.RulePanelReset;
        _reset.AccessibleName = RulePanelText.ResetName(_model.Title);
        _reset.AutoSize = true;
        _reset.Anchor = AnchorStyles.Right;
        _reset.TabIndex = 1;

        // No Alt key (UseMnemonic off): Tab reaches it after Reset, and Space or Enter toggles it, as for any button.
        _arrow.FlatStyle = FlatStyle.Flat;
        _arrow.FlatAppearance.BorderSize = 0;
        _arrow.AutoSize = true;
        _arrow.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _arrow.UseMnemonic = false;
        _arrow.Anchor = AnchorStyles.Right;
        _arrow.TabIndex = 2;
        ShowArrow(_collapse.IsCollapsed(_model));

        _content.Dock = DockStyle.Fill;
        _content.TabIndex = 3;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_check, 0, 0);
        layout.Controls.Add(_reset, 1, 0);
        layout.Controls.Add(_arrow, 2, 0);
        layout.Controls.Add(_content, 0, 1);
        layout.SetColumnSpan(_content, 3);
        Controls.Add(layout);
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void OnCollapseChanged(object? sender, IRulePanelModel panel)
    {
        if (ReferenceEquals(panel, _model))
        {
            ApplyCollapsed();
        }
    }

    /// <summary>Shows the collapse state: the arrow always, the content once started. Collapsing stores the open width
    /// as the minimum before the content hides (an AutoSize frame would shrink first), and never leaves the focus in the
    /// hidden content. Opening keeps that minimum. Collapsing never changes the rules.</summary>
    private void ApplyCollapsed()
    {
        bool collapsed = _collapse.IsCollapsed(_model);
        ShowArrow(collapsed);
        if (!_started || collapsed == _shownCollapsed)
        {
            return;
        }

        if (collapsed)
        {
            if (_content.ContainsFocus)
            {
                _arrow.Focus();
            }

            int openWidth = Math.Max(LogicalToDeviceUnits(MinimumLogicalSize.Width), GetPreferredSize(Size.Empty).Width);
            _openLogicalWidth = (int)Math.Round(openWidth * 96.0 / DeviceDpi);
            _shownCollapsed = true;
            MinimumSize = new Size(openWidth, 0);
            _content.Visible = false;
        }
        else
        {
            _shownCollapsed = false;
            _content.Visible = true;
        }

        BoxChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ShowArrow(bool collapsed)
    {
        _arrow.Text = PanelToggleText.Arrow(collapsed);
        _arrow.AccessibleName = PanelToggleText.Name(_model.Title, collapsed, CultureInfo.CurrentCulture);
    }

    /// <summary>Recreates the bold title font from the frame's current font, then disposes the old one once unused.</summary>
    private void RefreshBoldFont()
    {
        Font old = _boldFont;
        _boldFont = new Font(Font, FontStyle.Bold);
        Sync();
        old.Dispose();
    }

    private void Sync()
    {
        if (_check.Checked != _model.Enabled)
        {
            _check.Checked = _model.Enabled;
        }

        _content.Enabled = _model.Enabled;
        bool canReset = RulePanelHostModel.CanReset(_model);
        if (!canReset && _reset.Focused)
        {
            _check.Focus(); // a keyboard Reset disables the focused button: keep focus in the panel
        }

        _reset.Enabled = canReset;
        bool active = RulePanelHostModel.IsActive(_model);
        if (active)
        {
            _check.Font = _boldFont;
            // In a contrast theme the selection colour is a background colour: the bold title uses the text colour there.
            _check.ForeColor = SystemInformation.HighContrast ? SystemColors.ControlText : _accent;
        }
        else
        {
            _check.ResetFont(); // inherit the frame's font, so the title scales with it
            _check.ResetForeColor();
        }

        AccessibleDescription = active ? Strings.RulePanelActive : null;
        ApplyCollapsed();
        Invalidate();
    }
}
