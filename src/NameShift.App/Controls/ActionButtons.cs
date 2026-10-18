using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Actions;

namespace NameShift.App.Controls;

/// <summary>Reset, Revert and Rename stacked at the bottom right, with the hint that says why Rename is disabled.
/// Renders <see cref="MainActions"/> and forwards the clicks; no logic of its own.</summary>
internal sealed class ActionButtons : UserControl
{
    private const int HintLogicalWidth = 160;
    private static readonly Size ButtonLogicalSize = new Size(110, 28);

    private readonly MainActions _actions;
    private readonly Action<ActionReport> _showReport;
    private readonly Action<string> _showStatus;
    private readonly Button _reset = new Button();
    private readonly Button _revert = new Button();
    private readonly Button _rename = new Button();
    private readonly Label _hint = new Label();
    private readonly ToolTip _toolTip = new ToolTip();

    public ActionButtons(MainActions actions, Action<ActionReport> showReport, Action<string> showStatus)
    {
        _actions = actions ?? throw new ArgumentNullException(nameof(actions));
        _showReport = showReport ?? throw new ArgumentNullException(nameof(showReport));
        _showStatus = showStatus ?? throw new ArgumentNullException(nameof(showStatus));
        BuildLayout();
        _reset.Click += (_, _) => _actions.Reset();
        _revert.Click += OnRevertClick;
        _rename.Click += OnRenameClick;
        _actions.Changed += (_, _) => Render();
        Render();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        SetUpButton(_reset, Strings.ButtonReset);
        SetUpButton(_revert, Strings.ButtonRevert);
        SetUpButton(_rename, Strings.ButtonRename);
        _toolTip.SetToolTip(_reset, Strings.ResetDescription);

        _hint.AutoSize = true;
        _hint.MaximumSize = new Size(LogicalToDeviceUnits(HintLogicalWidth), 0);
        _hint.ForeColor = SystemColors.ControlText;

        var stack = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty,
        };
        stack.Controls.Add(_reset);
        stack.Controls.Add(_revert);
        stack.Controls.Add(_rename);
        stack.Controls.Add(_hint);

        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(stack);
    }

    private void SetUpButton(Button button, string text)
    {
        button.Text = text;
        button.AccessibleName = text;
        button.AutoSize = true;
        button.MinimumSize = LogicalToDeviceUnits(ButtonLogicalSize);
        button.UseMnemonic = false;
    }

    private void Render()
    {
        if (IsDisposed)
        {
            return;
        }

        _reset.Enabled = _actions.CanReset;
        _revert.Enabled = _actions.CanRevert;
        _rename.Enabled = _actions.CanRename;

        string hint = _actions.RenameHint;
        _hint.Text = hint;
        _hint.Visible = hint.Length > 0;
        _rename.AccessibleDescription = hint;
        _toolTip.SetToolTip(_revert, _actions.RevertDescription);
    }

    private async void OnRenameClick(object? sender, EventArgs e)
    {
        try
        {
            ActionReport? report = await _actions.RenameAsync(Confirm);
            if (report != null)
            {
                _showReport(report);
            }
        }
        catch (Exception)
        {
            // An exception escaping an async void handler would end the process.
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    private async void OnRevertClick(object? sender, EventArgs e)
    {
        try
        {
            ActionReport? report = await _actions.RevertAsync();
            if (report != null)
            {
                _showReport(report);
            }
        }
        catch (Exception)
        {
            // An exception escaping an async void handler would end the process.
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    private bool Confirm(string text) =>
        MessageBox.Show(FindForm(), text, Strings.AppTitle, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1)
        == DialogResult.Yes;
}
