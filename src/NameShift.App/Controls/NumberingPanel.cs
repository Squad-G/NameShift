using System;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Numbering panel: where the number goes, start, step, digits, separator and position.</summary>
internal sealed class NumberingPanel : UserControl
{
    private readonly NumberingPanelModel _model;
    private readonly ComboBox _mode = new ComboBox();
    private readonly TextBox _start = new TextBox();
    private readonly TextBox _step = new TextBox();
    private readonly TextBox _pad = new TextBox();
    private readonly TextBox _separator = new TextBox();
    private readonly TextBox _position = new TextBox();
    private readonly Label _positionLabel;
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public NumberingPanel(NumberingPanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _positionLabel = BuildLayout();
        Sync();

        _mode.SelectedIndexChanged += OnModeSelected;
        _start.TextChanged += (_, _) => _model.StartText = _start.Text;
        _step.TextChanged += (_, _) => _model.StepText = _step.Text;
        _pad.TextChanged += (_, _) => _model.PadText = _pad.Text;
        _separator.TextChanged += (_, _) => _model.Separator = _separator.Text;
        _position.TextChanged += (_, _) => _model.InsertPositionText = _position.Text;
        _model.Changed += OnModelChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _model.Changed -= OnModelChanged;
            _error.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <returns>The position label, greyed with its box.</returns>
    private Label BuildLayout()
    {
        TableLayoutPanel table = RulePanelLayout.CreateTable();

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var mode in NumberingPanelModel.Modes)
        {
            _mode.Items.Add(NumberingModeText.For(mode));
        }

        RulePanelLayout.AddLabeledRow(table, Strings.NumberingMode, _mode);
        RulePanelLayout.AddLabeledRow(table, Strings.NumberingStart, _start);
        RulePanelLayout.AddLabeledRow(table, Strings.NumberingStep, _step);
        RulePanelLayout.AddLabeledRow(table, Strings.NumberingPad, _pad);
        _separator.AccessibleDescription = Strings.NumberingSeparatorHint;
        RulePanelLayout.AddLabeledRow(table, Strings.NumberingSeparator, _separator);
        Label positionLabel = RulePanelLayout.AddLabeledRow(table, Strings.NumberingPosition, _position);

        foreach (Control field in new Control[] { _mode, _start, _step, _pad, _separator, _position })
        {
            field.Margin = new Padding(3, 3, LogicalToDeviceUnits(20), 3); // room for the error icon, same right edge for all
            _error.SetIconPadding(field, 2);
        }

        Controls.Add(table);
        return positionLabel;
    }

    private void OnModeSelected(object? sender, EventArgs e)
    {
        if (_mode.SelectedIndex >= 0)
        {
            _model.Mode = NumberingPanelModel.Modes[_mode.SelectedIndex];
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        int index = NumberingPanelModel.IndexOf(_model.Mode);
        if (_mode.SelectedIndex != index)
        {
            _mode.SelectedIndex = index;
        }

        RulePanelLayout.SetTextIfDifferent(_start, _model.StartText);
        RulePanelLayout.SetTextIfDifferent(_step, _model.StepText);
        RulePanelLayout.SetTextIfDifferent(_pad, _model.PadText);
        RulePanelLayout.SetTextIfDifferent(_separator, _model.Separator);
        RulePanelLayout.SetTextIfDifferent(_position, _model.InsertPositionText);
        _position.Enabled = _model.InsertPositionApplies;
        _positionLabel.Enabled = _model.InsertPositionApplies;

        RulePanelLayout.Mark(_error, _start, _model.StartError, null);
        RulePanelLayout.Mark(_error, _step, _model.StepError, null);
        RulePanelLayout.Mark(_error, _pad, _model.PadError, Strings.NumberingPadHint);
        RulePanelLayout.Mark(_error, _position, _model.InsertPositionError, Strings.NumberingPositionHint);
    }
}
