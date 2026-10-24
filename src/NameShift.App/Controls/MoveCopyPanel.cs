using System;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Move or copy panel: the action, where the characters are taken from, how many, where they go, the position and the text between.</summary>
internal sealed class MoveCopyPanel : UserControl
{
    private readonly MoveCopyPanelModel _model;
    private readonly ComboBox _mode = new ComboBox();
    private readonly ComboBox _source = new ComboBox();
    private readonly TextBox _count = new TextBox();
    private readonly ComboBox _target = new ComboBox();
    private readonly TextBox _position = new TextBox();
    private readonly TextBox _separator = new TextBox();
    private readonly Label _positionLabel;
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public MoveCopyPanel(MoveCopyPanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _positionLabel = BuildLayout();
        Sync();

        _mode.SelectedIndexChanged += OnModeSelected;
        _source.SelectedIndexChanged += OnSourceSelected;
        _count.TextChanged += (_, _) => _model.CountText = _count.Text;
        _target.SelectedIndexChanged += OnTargetSelected;
        _position.TextChanged += (_, _) => _model.PositionText = _position.Text;
        _separator.TextChanged += (_, _) => _model.Separator = _separator.Text;
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

        foreach (var mode in MoveCopyPanelModel.Modes)
        {
            _mode.Items.Add(MoveCopyModeText.For(mode));
        }

        foreach (var source in MoveCopyPanelModel.Sources)
        {
            _source.Items.Add(MoveCopySourceText.For(source));
        }

        foreach (var target in MoveCopyPanelModel.Targets)
        {
            _target.Items.Add(MoveCopyTargetText.For(target));
        }

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _source.DropDownStyle = ComboBoxStyle.DropDownList;
        _target.DropDownStyle = ComboBoxStyle.DropDownList;

        RulePanelLayout.AddLabeledRow(table, Strings.MoveCopyMode, _mode);
        RulePanelLayout.AddLabeledRow(table, Strings.MoveCopySource, _source);
        RulePanelLayout.AddLabeledRow(table, Strings.MoveCopyCount, _count);
        RulePanelLayout.AddLabeledRow(table, Strings.MoveCopyTarget, _target);
        Label positionLabel = RulePanelLayout.AddLabeledRow(table, Strings.MoveCopyPosition, _position);
        RulePanelLayout.AddLabeledRow(table, Strings.MoveCopySeparator, _separator);

        foreach (Control field in new Control[] { _mode, _source, _count, _target, _position, _separator })
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
            _model.Mode = MoveCopyPanelModel.Modes[_mode.SelectedIndex];
        }
    }

    private void OnSourceSelected(object? sender, EventArgs e)
    {
        if (_source.SelectedIndex >= 0)
        {
            _model.Source = MoveCopyPanelModel.Sources[_source.SelectedIndex];
        }
    }

    private void OnTargetSelected(object? sender, EventArgs e)
    {
        if (_target.SelectedIndex >= 0)
        {
            _model.Target = MoveCopyPanelModel.Targets[_target.SelectedIndex];
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        SelectIfDifferent(_mode, MoveCopyPanelModel.IndexOf(_model.Mode));
        SelectIfDifferent(_source, MoveCopyPanelModel.IndexOf(_model.Source));
        SelectIfDifferent(_target, MoveCopyPanelModel.IndexOf(_model.Target));
        RulePanelLayout.SetTextIfDifferent(_count, _model.CountText);
        RulePanelLayout.SetTextIfDifferent(_position, _model.PositionText);
        RulePanelLayout.SetTextIfDifferent(_separator, _model.Separator);
        _position.Enabled = _model.PositionApplies;
        _positionLabel.Enabled = _model.PositionApplies;

        RulePanelLayout.Mark(_error, _count, _model.CountInvalid ? Strings.MoveCopyNumberInvalid : "", null);
        RulePanelLayout.Mark(_error, _position, _model.PositionInvalid ? Strings.MoveCopyNumberInvalid : "", null);
    }

    private static void SelectIfDifferent(ComboBox box, int index)
    {
        if (box.SelectedIndex != index)
        {
            box.SelectedIndex = index;
        }
    }
}
