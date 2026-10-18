using System;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Replace panel: Find, With, Match case, Include extension.</summary>
internal sealed class ReplacePanel : UserControl
{
    private readonly ReplacePanelModel _model;
    private readonly TextBox _find = new TextBox();
    private readonly TextBox _with = new TextBox();
    private readonly CheckBox _matchCase = new CheckBox();
    private readonly CheckBox _includeExtension = new CheckBox();

    public ReplacePanel(ReplacePanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildLayout();
        Sync();

        _find.TextChanged += (_, _) => _model.Find = _find.Text;
        _with.TextChanged += (_, _) => _model.With = _with.Text;
        _matchCase.CheckedChanged += (_, _) => _model.MatchCase = _matchCase.Checked;
        _includeExtension.CheckedChanged += (_, _) => _model.IncludeExtension = _includeExtension.Checked;
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
        RulePanelLayout.AddLabeledRow(table, Strings.ReplaceFind, _find);
        RulePanelLayout.AddLabeledRow(table, Strings.ReplaceWith, _with);
        _matchCase.Text = Strings.ReplaceMatchCase;
        _matchCase.AutoSize = true;
        RulePanelLayout.AddSpanningRow(table, _matchCase);
        _includeExtension.Text = Strings.ReplaceIncludeExtension;
        _includeExtension.AutoSize = true;
        RulePanelLayout.AddSpanningRow(table, _includeExtension);
        Controls.Add(table);
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        RulePanelLayout.SetTextIfDifferent(_find, _model.Find);
        RulePanelLayout.SetTextIfDifferent(_with, _model.With);
        RulePanelLayout.SetCheckedIfDifferent(_matchCase, _model.MatchCase);
        RulePanelLayout.SetCheckedIfDifferent(_includeExtension, _model.IncludeExtension);
    }
}
