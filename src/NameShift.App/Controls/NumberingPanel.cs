using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Numbering panel: where the number goes, start, step, digits, separator and position.</summary>
internal sealed class NumberingPanel : UserControl
{
    private readonly NumberingPanelModel _model;
    private readonly ComboBox _mode = new ComboBox();
    private readonly ComboBox _system = new ComboBox();
    private readonly ComboBox _case = new ComboBox();
    private readonly TextBox _start = new TextBox();
    private readonly Label _startError = new Label();
    private readonly TextBox _step = new TextBox();
    private readonly TextBox _pad = new TextBox();
    private readonly TextBox _separator = new TextBox();
    private readonly TextBox _position = new TextBox();
    private readonly Label _positionLabel;
    private Label _padLabel = null!;
    private Label _caseLabel = null!;
    private const int ErrorLogicalWidth = 220;
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public NumberingPanel(NumberingPanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _positionLabel = BuildLayout();
        Sync();

        _mode.SelectedIndexChanged += OnModeSelected;
        _system.SelectedIndexChanged += OnSystemSelected;
        _case.SelectedIndexChanged += OnCaseSelected;
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

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyErrorWidth();
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
        _system.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var numerals in NumberingPanelModel.Systems)
        {
            _system.Items.Add(NumeralSystemText.For(numerals));
        }

        _case.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (bool upperCase in NumberingPanelModel.Cases)
        {
            _case.Items.Add(NumberingCaseText.For(upperCase));
        }

        RulePanelLayout.AddLabeledRow(table, Strings.NumberingSystem, _system);
        _caseLabel = RulePanelLayout.AddLabeledRow(table, Strings.NumberingCase, _case);
        RulePanelLayout.AddLabeledRow(table, Strings.NumberingStart, _start);
        _startError.AutoSize = true;
        _startError.UseMnemonic = false;
        _startError.Visible = false;
        ApplyErrorWidth();
        RulePanelLayout.AddSpanningRow(table, _startError);
        RulePanelLayout.AddLabeledRow(table, Strings.NumberingStep, _step);
        _padLabel = RulePanelLayout.AddLabeledRow(table, Strings.NumberingPad, _pad);
        _separator.AccessibleDescription = Strings.NumberingSeparatorHint;
        RulePanelLayout.AddLabeledRow(table, Strings.NumberingSeparator, _separator);
        Label positionLabel = RulePanelLayout.AddLabeledRow(table, Strings.NumberingPosition, _position);

        foreach (Control field in new Control[] { _mode, _system, _case, _start, _step, _pad, _separator, _position })
        {
            int small = LogicalToDeviceUnits(Spacing.Small);
            field.Margin = new Padding(small, small, LogicalToDeviceUnits(Spacing.Large + Spacing.Medium), small); // room for the error icon, same right edge for all
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

    private void OnSystemSelected(object? sender, EventArgs e)
    {
        if (_system.SelectedIndex >= 0)
        {
            _model.Numerals = NumberingPanelModel.Systems[_system.SelectedIndex];
        }
    }

    private void OnCaseSelected(object? sender, EventArgs e)
    {
        if (_case.SelectedIndex >= 0)
        {
            _model.UpperCase = NumberingPanelModel.Cases[_case.SelectedIndex];
        }
    }

    /// <summary>Lets the start error wrap within the width of the other panels.</summary>
    private void ApplyErrorWidth() => _startError.MaximumSize = new Size(LogicalToDeviceUnits(ErrorLogicalWidth), 0);

    private static void SelectIfDifferent(ComboBox box, int index)
    {
        if (box.SelectedIndex != index)
        {
            box.SelectedIndex = index;
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

        SelectIfDifferent(_system, NumberingPanelModel.IndexOf(_model.Numerals));
        SelectIfDifferent(_case, NumberingPanelModel.IndexOfCase(_model.UpperCase));
        _case.Enabled = _model.CaseApplies;
        _caseLabel.Enabled = _model.CaseApplies;
        _pad.Enabled = _model.PadApplies;
        _padLabel.Enabled = _model.PadApplies;
        RulePanelLayout.SetTextIfDifferent(_start, _model.StartText);
        RulePanelLayout.SetTextIfDifferent(_step, _model.StepText);
        RulePanelLayout.SetTextIfDifferent(_pad, _model.PadText);
        RulePanelLayout.SetTextIfDifferent(_separator, _model.Separator);
        RulePanelLayout.SetTextIfDifferent(_position, _model.InsertPositionText);
        _position.Enabled = _model.InsertPositionApplies;
        _positionLabel.Enabled = _model.InsertPositionApplies;

        string startError = _model.StartError;
        RulePanelLayout.Mark(_error, _start, startError, null);
        RulePanelLayout.SetTextIfDifferent(_startError, startError);
        _startError.Visible = startError.Length > 0;
        RulePanelLayout.Mark(_error, _step, _model.StepError, null);
        RulePanelLayout.Mark(_error, _pad, _model.PadError, Strings.NumberingPadHint);
        RulePanelLayout.Mark(_error, _position, _model.InsertPositionError, Strings.NumberingPositionHint);
    }
}
