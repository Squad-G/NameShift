using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Add panel: prefix, insert with its position, suffix and the word-space option.</summary>
internal sealed class AddPanel : UserControl
{
    private readonly AddPanelModel _model;
    private readonly TextBox _prefix = new TextBox();
    private readonly TextBox _insert = new TextBox();
    private readonly Label _positionLabel = new Label();
    private readonly TextBox _position = new TextBox();
    private readonly CheckBox _fromEnd = new CheckBox();
    private readonly TextBox _suffix = new TextBox();
    private readonly CheckBox _wordSpace = new CheckBox();
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public AddPanel(AddPanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildLayout();
        FitPositionBox();
        Sync();

        _prefix.TextChanged += (_, _) => _model.Prefix = _prefix.Text;
        _insert.TextChanged += (_, _) => _model.Insert = _insert.Text;
        _position.TextChanged += (_, _) => _model.InsertPositionText = _position.Text;
        _fromEnd.CheckedChanged += (_, _) => _model.InsertFromEnd = _fromEnd.Checked;
        _suffix.TextChanged += (_, _) => _model.Suffix = _suffix.Text;
        _wordSpace.CheckedChanged += (_, _) => _model.WordSpace = _wordSpace.Checked;
        _model.Changed += OnModelChanged;
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        FitPositionBox();
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
        TableLayoutPanel table = RulePanelLayout.CreateTable();
        RulePanelLayout.AddLabeledRow(table, Strings.AddPrefix, _prefix);
        RulePanelLayout.AddLabeledRow(table, Strings.AddInsert, _insert);

        _positionLabel.AutoSize = true;
        _positionLabel.UseMnemonic = true;
        _positionLabel.Anchor = AnchorStyles.Left;
        _positionLabel.TabIndex = 0;
        _position.TabIndex = 1;
        _position.Margin = new Padding(3, 3, LogicalToDeviceUnits(20), 3); // room for the error icon
        _fromEnd.Text = Strings.AddFromEnd;
        _fromEnd.AutoSize = true;
        _fromEnd.TabIndex = 2;
        var positionRow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = Padding.Empty,
        };
        positionRow.Controls.Add(_positionLabel);
        positionRow.Controls.Add(_position);
        positionRow.Controls.Add(_fromEnd);
        RulePanelLayout.AddSpanningRow(table, positionRow);
        _error.SetIconPadding(_position, 2);

        RulePanelLayout.AddLabeledRow(table, Strings.AddSuffix, _suffix);
        _wordSpace.Text = Strings.AddWordSpace;
        _wordSpace.AutoSize = true;
        _wordSpace.AccessibleDescription = Strings.AddWordSpaceHint;
        RulePanelLayout.AddSpanningRow(table, _wordSpace);
        Controls.Add(table);
    }

    /// <summary>Sizes the position box from its font, so it holds a few digits at any DPI.</summary>
    private void FitPositionBox()
    {
        _position.Width = TextRenderer.MeasureText("00000", _position.Font).Width + 8;
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        RulePanelLayout.SetTextIfDifferent(_prefix, _model.Prefix);
        RulePanelLayout.SetTextIfDifferent(_insert, _model.Insert);
        RulePanelLayout.SetTextIfDifferent(_position, _model.InsertPositionText);
        RulePanelLayout.SetCheckedIfDifferent(_fromEnd, _model.InsertFromEnd);
        RulePanelLayout.SetTextIfDifferent(_suffix, _model.Suffix);
        RulePanelLayout.SetCheckedIfDifferent(_wordSpace, _model.WordSpace);
        RulePanelLayout.SetTextIfDifferent(_positionLabel, _model.InsertPositionLabel);
        _position.AccessibleName = RulePanelText.PlainTitle(_model.InsertPositionLabel);

        RulePanelLayout.Mark(_error, _position, _model.InsertPositionInvalid ? Strings.AddPositionInvalid : "", null);
    }
}
