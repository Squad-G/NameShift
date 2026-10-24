using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Presets;
using NameShift.Ui.Rules;

namespace NameShift.App.Dialogs;

/// <summary>Asks for the name of a preset: a label with an Alt key, a text box limited to
/// <see cref="PresetNames.MaxLength"/> characters, OK (disabled while the name is empty or too long) and Cancel.</summary>
internal sealed class PresetNameDialog : Form
{
    private const int BoxLogicalWidth = 300;
    private static readonly Size ButtonLogicalSize = new Size(80, 28);

    private readonly TextBox _box = new TextBox();
    private readonly Button _ok = new Button();

    private PresetNameDialog(string preFill)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = Strings.PresetNameDialogTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var label = new Label
        {
            Text = Strings.PresetNameLabel,
            AutoSize = true,
            UseMnemonic = true,
            Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(Spacing.Small)),
        };
        _box.MaxLength = PresetNames.MaxLength;
        _box.Width = LogicalToDeviceUnits(BoxLogicalWidth);
        _box.AccessibleName = RulePanelText.PlainTitle(Strings.PresetNameLabel);
        _box.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(Spacing.Large));
        _box.Text = preFill;
        _box.SelectAll();
        _box.TextChanged += (_, _) => UpdateOk();

        SetUpButton(_ok, Strings.DialogOk, DialogResult.OK);
        var cancel = new Button();
        SetUpButton(cancel, Strings.DialogCancel, DialogResult.Cancel);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Right,
        };
        buttons.Controls.Add(_ok);
        buttons.Controls.Add(cancel);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 3,
            Dock = DockStyle.Fill,
            Padding = new Padding(LogicalToDeviceUnits(Spacing.Large)),
        };

        // The label comes just before the box in the tab order: its Alt key focuses the box.
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_box, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        AcceptButton = _ok;
        CancelButton = cancel;
        ActiveControl = _box;
        UpdateOk();
    }

    /// <summary>Asks for a name; the text on OK, null on Cancel (or Escape).</summary>
    public static string? Ask(IWin32Window owner, string preFill)
    {
        using var dialog = new PresetNameDialog(preFill);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._box.Text : null;
    }

    private void SetUpButton(Button button, string text, DialogResult result)
    {
        button.Text = text;
        button.AccessibleName = RulePanelText.PlainTitle(text);
        button.DialogResult = result;
        button.AutoSize = true;
        button.MinimumSize = LogicalToDeviceUnits(ButtonLogicalSize);
        button.Margin = new Padding(LogicalToDeviceUnits(Spacing.Small), 0, 0, 0);
    }

    private void UpdateOk() => _ok.Enabled = PresetNames.Check(_box.Text).Verdict == PresetNameVerdict.Ok;
}
