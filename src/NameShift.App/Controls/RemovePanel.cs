using System;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Remove panel: four groups (position, text, character types, tidy) of one stage.</summary>
internal sealed class RemovePanel : UserControl
{
    private readonly RemovePanelModel _model;
    private readonly TextBox _first = new TextBox();
    private readonly TextBox _last = new TextBox();
    private readonly TextBox _from = new TextBox();
    private readonly TextBox _to = new TextBox();
    private readonly TextBox _characters = new TextBox();
    private readonly TextBox _text = new TextBox();
    private readonly CheckBox _wholeWord = new CheckBox();
    private readonly CheckBox _matchCase = new CheckBox();
    private readonly TextBox _cropBefore = new TextBox();
    private readonly TextBox _cropAfter = new TextBox();
    private readonly CheckBox _keepMarker = new CheckBox();
    private readonly CheckBox _digits = new CheckBox();
    private readonly CheckBox _symbols = new CheckBox();
    private readonly CheckBox _accents = new CheckBox();
    private readonly CheckBox _highCharacters = new CheckBox();
    private readonly CheckBox _trim = new CheckBox();
    private readonly CheckBox _leadingDots = new CheckBox();
    private readonly CheckBox _doubleSpaces = new CheckBox();
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public RemovePanel(RemovePanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildLayout();
        Sync();

        _first.TextChanged += (_, _) => _model.FirstText = _first.Text;
        _last.TextChanged += (_, _) => _model.LastText = _last.Text;
        _from.TextChanged += (_, _) => _model.FromText = _from.Text;
        _to.TextChanged += (_, _) => _model.ToText = _to.Text;
        _characters.TextChanged += (_, _) => _model.Characters = _characters.Text;
        _text.TextChanged += (_, _) => _model.Text = _text.Text;
        _wholeWord.CheckedChanged += (_, _) => _model.WholeWord = _wholeWord.Checked;
        _matchCase.CheckedChanged += (_, _) => _model.MatchCase = _matchCase.Checked;
        _cropBefore.TextChanged += (_, _) => _model.CropBefore = _cropBefore.Text;
        _cropAfter.TextChanged += (_, _) => _model.CropAfter = _cropAfter.Text;
        _keepMarker.CheckedChanged += (_, _) => _model.KeepMarker = _keepMarker.Checked;
        _digits.CheckedChanged += (_, _) => _model.Digits = _digits.Checked;
        _symbols.CheckedChanged += (_, _) => _model.Symbols = _symbols.Checked;
        _accents.CheckedChanged += (_, _) => _model.Accents = _accents.Checked;
        _highCharacters.CheckedChanged += (_, _) => _model.HighCharacters = _highCharacters.Checked;
        _trim.CheckedChanged += (_, _) => _model.Trim = _trim.Checked;
        _leadingDots.CheckedChanged += (_, _) => _model.LeadingDots = _leadingDots.Checked;
        _doubleSpaces.CheckedChanged += (_, _) => _model.DoubleSpaces = _doubleSpaces.Checked;
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

    private void BuildLayout()
    {
        TableLayoutPanel outer = RulePanelLayout.CreateTable();

        TableLayoutPanel position = AddGroup(outer, Strings.RemoveGroupPosition);
        RulePanelLayout.AddLabeledRow(position, Strings.RemoveFirst, _first);
        RulePanelLayout.AddLabeledRow(position, Strings.RemoveLast, _last);
        RulePanelLayout.AddLabeledRow(position, Strings.RemoveFrom, _from);
        RulePanelLayout.AddLabeledRow(position, Strings.RemoveTo, _to);
        foreach (TextBox box in new[] { _first, _last, _from, _to })
        {
            box.Margin = new Padding(3, 3, LogicalToDeviceUnits(20), 3); // room for the error icon, same right edge for all
            _error.SetIconPadding(box, 2);
        }

        TableLayoutPanel text = AddGroup(outer, Strings.RemoveGroupText);
        _characters.AccessibleDescription = Strings.RemoveCharactersHint;
        RulePanelLayout.AddLabeledRow(text, Strings.RemoveCharacters, _characters);
        RulePanelLayout.AddLabeledRow(text, Strings.RemoveText, _text);
        AddCheckBox(text, _wholeWord, Strings.RemoveWholeWord);
        AddCheckBox(text, _matchCase, Strings.RemoveMatchCase);
        RulePanelLayout.AddLabeledRow(text, Strings.RemoveCropBefore, _cropBefore);
        RulePanelLayout.AddLabeledRow(text, Strings.RemoveCropAfter, _cropAfter);
        AddCheckBox(text, _keepMarker, Strings.RemoveKeepMarker);

        TableLayoutPanel classes = AddGroup(outer, Strings.RemoveGroupClasses);
        AddCheckBox(classes, _digits, Strings.RemoveDigits);
        AddCheckBox(classes, _symbols, Strings.RemoveSymbols);
        AddCheckBox(classes, _accents, Strings.RemoveAccents);
        AddCheckBox(classes, _highCharacters, Strings.RemoveHighCharacters);

        TableLayoutPanel tidy = AddGroup(outer, Strings.RemoveGroupTidy);
        AddCheckBox(tidy, _trim, Strings.RemoveTrim);
        AddCheckBox(tidy, _leadingDots, Strings.RemoveLeadingDots);
        AddCheckBox(tidy, _doubleSpaces, Strings.RemoveDoubleSpaces);

        Controls.Add(outer);
    }

    /// <summary>Adds a captioned group as a spanning row of <paramref name="outer"/>.</summary>
    /// <returns>The table inside the group, for its rows.</returns>
    private static TableLayoutPanel AddGroup(TableLayoutPanel outer, string caption)
    {
        var group = new GroupBox
        {
            Text = caption,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        TableLayoutPanel table = RulePanelLayout.CreateTable();
        group.Controls.Add(table);
        RulePanelLayout.AddSpanningRow(outer, group);
        group.Dock = DockStyle.Fill;
        return table;
    }

    private static void AddCheckBox(TableLayoutPanel table, CheckBox box, string text)
    {
        box.Text = text;
        box.AutoSize = true;
        RulePanelLayout.AddSpanningRow(table, box);
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        RulePanelLayout.SetTextIfDifferent(_first, _model.FirstText);
        RulePanelLayout.SetTextIfDifferent(_last, _model.LastText);
        RulePanelLayout.SetTextIfDifferent(_from, _model.FromText);
        RulePanelLayout.SetTextIfDifferent(_to, _model.ToText);
        RulePanelLayout.SetTextIfDifferent(_characters, _model.Characters);
        RulePanelLayout.SetTextIfDifferent(_text, _model.Text);
        RulePanelLayout.SetCheckedIfDifferent(_wholeWord, _model.WholeWord);
        RulePanelLayout.SetCheckedIfDifferent(_matchCase, _model.MatchCase);
        RulePanelLayout.SetTextIfDifferent(_cropBefore, _model.CropBefore);
        RulePanelLayout.SetTextIfDifferent(_cropAfter, _model.CropAfter);
        RulePanelLayout.SetCheckedIfDifferent(_keepMarker, _model.KeepMarker);
        RulePanelLayout.SetCheckedIfDifferent(_digits, _model.Digits);
        RulePanelLayout.SetCheckedIfDifferent(_symbols, _model.Symbols);
        RulePanelLayout.SetCheckedIfDifferent(_accents, _model.Accents);
        RulePanelLayout.SetCheckedIfDifferent(_highCharacters, _model.HighCharacters);
        RulePanelLayout.SetCheckedIfDifferent(_trim, _model.Trim);
        RulePanelLayout.SetCheckedIfDifferent(_leadingDots, _model.LeadingDots);
        RulePanelLayout.SetCheckedIfDifferent(_doubleSpaces, _model.DoubleSpaces);
        _wholeWord.Enabled = _model.WholeWordApplies;
        _matchCase.Enabled = _model.MatchCaseApplies;
        _keepMarker.Enabled = _model.KeepMarkerApplies;

        string rangeError = _model.RangeInvalid ? Strings.RemoveRangeInvalid : "";
        RulePanelLayout.Mark(_error, _first, _model.FirstInvalid ? Strings.RemovePositionInvalid : "", null);
        RulePanelLayout.Mark(_error, _last, _model.LastInvalid ? Strings.RemovePositionInvalid : "", null);
        RulePanelLayout.Mark(_error, _from, rangeError, Strings.RemoveRangeHint);
        RulePanelLayout.Mark(_error, _to, rangeError, Strings.RemoveRangeHint);
    }
}
