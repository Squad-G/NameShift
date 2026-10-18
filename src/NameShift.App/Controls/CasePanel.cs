using System;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Case panel: the mode list and the exceptions (used in Title Case only).</summary>
internal sealed class CasePanel : UserControl
{
    private readonly CasePanelModel _model;
    private readonly ComboBox _mode = new ComboBox();
    private readonly TextBox _exceptions = new TextBox();

    public CasePanel(CasePanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildLayout();
        Sync();

        _mode.SelectedIndexChanged += OnModeSelected;
        _exceptions.TextChanged += (_, _) => _model.ExceptionsText = _exceptions.Text;
        _model.Changed += OnModelChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _model.Changed -= OnModelChanged;
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        TableLayoutPanel table = RulePanelLayout.CreateTable();

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var mode in CasePanelModel.Modes)
        {
            _mode.Items.Add(CaseModeText.For(mode));
        }

        _mode.AccessibleName = RulePanelText.PlainTitle(Strings.CaseMode);
        RulePanelLayout.AddTextRow(table, new Label { Text = Strings.CaseMode }, _mode);

        _exceptions.AccessibleName = RulePanelText.PlainTitle(Strings.CaseExceptions);
        _exceptions.AccessibleDescription = Strings.CaseExceptionsHint;
        RulePanelLayout.AddTextRow(table, new Label { Text = Strings.CaseExceptions }, _exceptions);
        Controls.Add(table);
    }

    private void OnModeSelected(object? sender, EventArgs e)
    {
        if (_mode.SelectedIndex >= 0)
        {
            _model.Mode = CasePanelModel.Modes[_mode.SelectedIndex];
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        int index = IndexOfMode(_model.Mode);
        if (_mode.SelectedIndex != index)
        {
            _mode.SelectedIndex = index;
        }

        RulePanelLayout.SetTextIfDifferent(_exceptions, _model.ExceptionsText);
        _exceptions.Enabled = _model.ExceptionsApply;
    }

    private static int IndexOfMode(Core.CaseMode mode)
    {
        for (int i = 0; i < CasePanelModel.Modes.Count; i++)
        {
            if (CasePanelModel.Modes[i] == mode)
            {
                return i;
            }
        }

        return -1;
    }
}
