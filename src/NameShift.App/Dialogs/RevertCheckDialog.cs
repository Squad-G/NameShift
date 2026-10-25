using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using NameShift.Disk;
using NameShift.Ui;
using NameShift.Ui.Actions;
using NameShift.Ui.Rules;

namespace NameShift.App.Dialogs;

/// <summary>Lists the files a Revert would skip and asks what to do: Revert the rest, Revert changed files too (each only
/// when it applies) and Cancel, or Close alone when nothing can run. Esc and the close box are Cancel.</summary>
internal sealed class RevertCheckDialog : Form
{
    private const int ListLogicalWidth = 420;
    private static readonly Size ButtonLogicalSize = new Size(80, 28);

    private RevertCheckDialog(RevertCheck check, RevertDialogOptions options)
    {
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = Strings.ButtonRevert;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        var title = new Label
        {
            Text = Strings.RevertCheckTitle,
            AutoSize = true,
            MaximumSize = new Size(LogicalToDeviceUnits(ListLogicalWidth), 0),
            Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(Spacing.Small)),
        };
        var list = new Label
        {
            Text = string.Join("\r\n", RevertCheckText.Lines(check, CultureInfo.CurrentCulture)),
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(LogicalToDeviceUnits(ListLogicalWidth), 0),
            AccessibleName = RulePanelText.PlainTitle(Strings.RevertCheckTitle),
            Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(Spacing.Large)),
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
            Anchor = AnchorStyles.Right,
        };

        Button? rest = null;
        if (options.ShowRevertRest)
        {
            rest = new Button();
            SetUpButton(rest, Strings.RevertCheckRest, DialogResult.OK);
            buttons.Controls.Add(rest);
        }

        if (options.ShowIncludeModified)
        {
            var include = new Button();
            SetUpButton(include, Strings.RevertCheckIncludeModified, DialogResult.Yes);
            buttons.Controls.Add(include);
        }

        var cancel = new Button();
        SetUpButton(cancel, options.CloseOnly ? Strings.RevertCheckClose : Strings.RevertCheckCancel, DialogResult.Cancel);
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
        layout.Controls.Add(title, 0, 0);
        layout.Controls.Add(list, 0, 1);
        layout.Controls.Add(buttons, 0, 2);
        Controls.Add(layout);

        // The safe default: the plain Revert, or Cancel when that is not offered. Never the override.
        AcceptButton = rest ?? cancel;
        CancelButton = cancel;
        ActiveControl = rest ?? cancel;
    }

    /// <summary>Shows the problems and the buttons that apply. Anything but a button's own result (Esc, the close box,
    /// Cancel, Close) is Cancel.</summary>
    public static RevertDialogChoice Ask(IWin32Window owner, RevertCheck check)
    {
        RevertDialogOptions options = RevertDialogOptions.For(check);
        using var dialog = new RevertCheckDialog(check, options);
        switch (dialog.ShowDialog(owner))
        {
            case DialogResult.OK:
                return RevertDialogChoice.RevertRest;
            case DialogResult.Yes:
                return RevertDialogChoice.IncludeModified;
            default:
                return RevertDialogChoice.Cancel;
        }
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
}
