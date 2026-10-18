using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>
/// The frame around one rule panel: stage number and title as the enable checkbox, a Reset button, the content below.
/// An active panel (enabled with a non-default setting) gets a thicker accent border and a bold title: not colour alone.
/// </summary>
internal sealed class RulePanelFrame : UserControl
{
    private static readonly Size MinimumLogicalSize = new Size(240, 0);

    private readonly IRulePanelModel _model;
    private readonly Control _content;
    private readonly CheckBox _check = new CheckBox();
    private readonly Button _reset = new Button();
    private Font _boldFont;

    public RulePanelFrame(IRulePanelModel model, Control content)
    {
        _model = model;
        _content = content;
        _boldFont = new Font(Font, FontStyle.Bold);
        ResizeRedraw = true;
        DoubleBuffered = true;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ApplyLogicalMetrics();
        BuildLayout();
        Sync();

        _check.CheckedChanged += (_, _) => _model.Enabled = _check.Checked;
        _reset.Click += (_, _) => _model.Reset();
        _model.Changed += OnModelChanged;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        bool active = RulePanelHostModel.IsActive(_model);
        int width = active ? Math.Max(2, LogicalToDeviceUnits(2)) : Math.Max(1, LogicalToDeviceUnits(1));
        Color color = active ? SystemColors.Highlight : SystemColors.ControlDark;
        Rectangle area = Rectangle.Inflate(ClientRectangle, -LogicalToDeviceUnits(1), -LogicalToDeviceUnits(1));
        using (var pen = new Pen(color, width) { Alignment = System.Drawing.Drawing2D.PenAlignment.Inset })
        {
            e.Graphics.DrawRectangle(pen, area.X, area.Y, area.Width - 1, area.Height - 1);
        }
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Font old = _boldFont;
        _boldFont = new Font(Font, FontStyle.Bold);
        old.Dispose();
        Sync();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyLogicalMetrics();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _model.Changed -= OnModelChanged;
            _boldFont.Dispose();
        }

        base.Dispose(disposing);
    }

    private void ApplyLogicalMetrics()
    {
        MinimumSize = LogicalToDeviceUnits(MinimumLogicalSize);
        Margin = new Padding(LogicalToDeviceUnits(4));
        Padding = new Padding(LogicalToDeviceUnits(3));
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

        _content.Dock = DockStyle.Fill;
        _content.TabIndex = 2;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_check, 0, 0);
        layout.Controls.Add(_reset, 1, 0);
        layout.Controls.Add(_content, 0, 1);
        layout.SetColumnSpan(_content, 2);
        Controls.Add(layout);
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        if (_check.Checked != _model.Enabled)
        {
            _check.Checked = _model.Enabled;
        }

        _content.Enabled = _model.Enabled;
        _reset.Enabled = RulePanelHostModel.CanReset(_model);
        bool active = RulePanelHostModel.IsActive(_model);
        _check.Font = active ? _boldFont : Font;
        AccessibleDescription = active ? Strings.RulePanelActive : null;
        Invalidate();
    }
}