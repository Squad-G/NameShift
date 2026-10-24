using System;
using System.Windows.Forms;
using NameShift.Core;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Name panel: the mode list and the text (used in Fixed mode only).</summary>
internal sealed class NamePanel : UserControl
{
    private readonly NamePanelModel _model;
    private readonly ComboBox _mode = new ComboBox();
    private readonly TextBox _text = new TextBox();
    private readonly Label _textLabel;

    public NamePanel(NamePanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _textLabel = BuildLayout();
        Sync();

        _mode.SelectedIndexChanged += OnModeSelected;
        _text.TextChanged += (_, _) => _model.Text = _text.Text;
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

    /// <returns>The Text label, greyed with its box.</returns>
    private Label BuildLayout()
    {
        TableLayoutPanel table = RulePanelLayout.CreateTable();

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var mode in NamePanelModel.Modes)
        {
            _mode.Items.Add(NameModeText.For(mode));
        }

        RulePanelLayout.AddLabeledRow(table, Strings.NameMode, _mode);

        Label textLabel = RulePanelLayout.AddLabeledRow(table, Strings.NameText, _text);
        Controls.Add(table);
        return textLabel;
    }

    private void OnModeSelected(object? sender, EventArgs e)
    {
        if (_mode.SelectedIndex >= 0)
        {
            _model.Mode = NamePanelModel.Modes[_mode.SelectedIndex];
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        int index = NamePanelModel.IndexOf(_model.Mode);
        if (_mode.SelectedIndex != index)
        {
            _mode.SelectedIndex = index;
        }

        RulePanelLayout.SetTextIfDifferent(_text, _model.Text);
        _text.Enabled = _model.TextApplies;
        _textLabel.Enabled = _model.TextApplies;
    }
}
