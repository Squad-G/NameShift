using System;
using System.Windows.Forms;
using NameShift.Core;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Case panel: the mode list and the exceptions (used in Title Case only).</summary>
internal sealed class CasePanel : UserControl
{
    private readonly CasePanelModel _model;
    private readonly ComboBox _mode = new ComboBox();
    private readonly TextBox _exceptions = new TextBox();
    private readonly Label _exceptionsLabel;

    public CasePanel(CasePanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _exceptionsLabel = BuildLayout();
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

    /// <returns>The Exceptions label, greyed with its box.</returns>
    private Label BuildLayout()
    {
        TableLayoutPanel table = RulePanelLayout.CreateTable();

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var mode in CasePanelModel.Modes)
        {
            _mode.Items.Add(CaseModeText.For(mode));
        }

        RulePanelLayout.AddLabeledRow(table, Strings.CaseMode, _mode);

        _exceptions.AccessibleDescription = Strings.CaseExceptionsHint;
        Label exceptionsLabel = RulePanelLayout.AddLabeledRow(table, Strings.CaseExceptions, _exceptions);
        Controls.Add(table);
        return exceptionsLabel;
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
        int index = CasePanelModel.IndexOf(_model.Mode);
        if (_mode.SelectedIndex != index)
        {
            _mode.SelectedIndex = index;
        }

        RulePanelLayout.SetTextIfDifferent(_exceptions, _model.ExceptionsText);
        _exceptions.Enabled = _model.ExceptionsApply;
        _exceptionsLabel.Enabled = _model.ExceptionsApply;
    }
}
