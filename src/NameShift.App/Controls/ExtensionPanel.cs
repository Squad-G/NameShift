using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Core;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Extension panel: the mode list, the text (Fixed and Extra only) and a note that the content is not converted.</summary>
internal sealed class ExtensionPanel : UserControl
{
    private readonly ExtensionPanelModel _model;
    private readonly ComboBox _mode = new ComboBox();
    private readonly TextBox _text = new TextBox();
    private readonly Label _textLabel;

    public ExtensionPanel(ExtensionPanelModel model)
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
        foreach (var mode in ExtensionPanelModel.Modes)
        {
            _mode.Items.Add(ExtensionModeText.For(mode));
        }

        RulePanelLayout.AddLabeledRow(table, Strings.ExtensionMode, _mode);

        Label textLabel = RulePanelLayout.AddLabeledRow(table, Strings.ExtensionText, _text);

        var note = new Label
        {
            Text = Strings.ExtensionNote,
            AutoSize = true,
            UseMnemonic = false,
            ForeColor = SystemColors.GrayText,
        };
        RulePanelLayout.AddSpanningRow(table, note);
        Controls.Add(table);
        return textLabel;
    }

    private void OnModeSelected(object? sender, EventArgs e)
    {
        if (_mode.SelectedIndex >= 0)
        {
            _model.Mode = ExtensionPanelModel.Modes[_mode.SelectedIndex];
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        int index = ExtensionPanelModel.IndexOf(_model.Mode);
        if (_mode.SelectedIndex != index)
        {
            _mode.SelectedIndex = index;
        }

        RulePanelLayout.SetTextIfDifferent(_text, _model.Text);
        _text.Enabled = _model.TextApplies;
        _textLabel.Enabled = _model.TextApplies;
    }
}
