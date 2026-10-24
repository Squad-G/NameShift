using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Dialogs;

/// <summary>A Yes/No question. A <see cref="MessageBox"/> with Yes and No ignores Escape (it has no Cancel button), so
/// this small form makes Escape and the close box mean No. The default button has the focus and answers Enter.</summary>
internal sealed class YesNoDialog : Form
{
    private const int TextLogicalWidth = 360;
    private static readonly Size ButtonLogicalSize = new Size(80, 28);

    private YesNoDialog(string question, bool yesIsDefault)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = Strings.AppTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var label = new Label
        {
            Text = question,
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(LogicalToDeviceUnits(TextLogicalWidth), 0),
            Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(Spacing.Large)),
        };
        Button yes = NewButton(Strings.DialogYes, DialogResult.Yes);
        Button no = NewButton(Strings.DialogNo, DialogResult.No);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Right,
        };
        buttons.Controls.Add(yes);
        buttons.Controls.Add(no);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            Dock = DockStyle.Fill,
            Padding = new Padding(LogicalToDeviceUnits(Spacing.Large)),
        };
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(buttons, 0, 1);
        Controls.Add(layout);

        CancelButton = no;
        Button defaultButton = yesIsDefault ? yes : no;
        AcceptButton = defaultButton;
        ActiveControl = defaultButton;
    }

    /// <summary>Asks the question; true only for Yes (Escape, the close box and No are false).</summary>
    public static bool Ask(IWin32Window owner, string question, bool yesIsDefault)
    {
        using var dialog = new YesNoDialog(question, yesIsDefault);
        return dialog.ShowDialog(owner) == DialogResult.Yes;
    }

    private Button NewButton(string text, DialogResult result) => new Button
    {
        Text = text,
        AccessibleName = RulePanelText.PlainTitle(text),
        DialogResult = result,
        AutoSize = true,
        MinimumSize = LogicalToDeviceUnits(ButtonLogicalSize),
        Margin = new Padding(LogicalToDeviceUnits(Spacing.Small), 0, 0, 0),
    };
}
