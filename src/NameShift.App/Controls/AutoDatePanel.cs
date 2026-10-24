using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Auto date panel: the date source, the format (an editable list) with its error and live example, where the date goes and the text between.</summary>
internal sealed class AutoDatePanel : UserControl
{
    private const int LabelLogicalWidth = 220;

    private readonly AutoDatePanelModel _model;
    private readonly ComboBox _source = new ComboBox();
    private readonly ComboBox _format = new ComboBox();
    private readonly ComboBox _place = new ComboBox();
    private readonly TextBox _separator = new TextBox();
    private readonly Label _errorText = new Label();
    private readonly Label _exampleText = new Label();
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public AutoDatePanel(AutoDatePanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildLayout();
        Sync();

        _source.SelectedIndexChanged += OnSourceSelected;
        _format.TextChanged += (_, _) => _model.Format = _format.Text;
        _format.SelectedIndexChanged += OnFormatSelected;
        _place.SelectedIndexChanged += OnPlaceSelected;
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

    private void BuildLayout()
    {
        TableLayoutPanel table = RulePanelLayout.CreateTable();

        foreach (var source in AutoDatePanelModel.Sources)
        {
            _source.Items.Add(DateSourceText.For(source));
        }

        foreach (string format in AutoDatePanelModel.CommonFormats)
        {
            _format.Items.Add(format);
        }

        foreach (var placement in AutoDatePanelModel.Placements)
        {
            _place.Items.Add(DatePlacementText.For(placement));
        }

        _source.DropDownStyle = ComboBoxStyle.DropDownList;
        _format.DropDownStyle = ComboBoxStyle.DropDown;
        _place.DropDownStyle = ComboBoxStyle.DropDownList;

        RulePanelLayout.AddLabeledRow(table, Strings.AutoDateSource, _source);
        RulePanelLayout.AddLabeledRow(table, Strings.AutoDateFormat, _format);

        _errorText.AutoSize = true;
        _errorText.UseMnemonic = false;
        _errorText.Visible = false;
        RulePanelLayout.AddSpanningRow(table, _errorText);

        _exampleText.AutoSize = true;
        _exampleText.UseMnemonic = false; // an example can hold an ampersand, from a quoted literal
        _exampleText.Visible = false;
        RulePanelLayout.AddSpanningRow(table, _exampleText);
        ApplyLabelWidth();

        RulePanelLayout.AddLabeledRow(table, Strings.AutoDatePlace, _place);
        RulePanelLayout.AddLabeledRow(table, Strings.AutoDateSeparator, _separator);

        foreach (Control field in new Control[] { _source, _format, _place, _separator })
        {
            field.Margin = new Padding(3, 3, LogicalToDeviceUnits(20), 3); // room for the error icon, same right edge for all
        }

        _error.SetIconPadding(_format, 2);
        Controls.Add(table);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyLabelWidth();
    }

    /// <summary>Lets the error and example texts wrap within the width of the other panels.</summary>
    private void ApplyLabelWidth()
    {
        var maximum = new Size(LogicalToDeviceUnits(LabelLogicalWidth), 0);
        _errorText.MaximumSize = maximum;
        _exampleText.MaximumSize = maximum;
    }

    private void OnSourceSelected(object? sender, EventArgs e)
    {
        if (_source.SelectedIndex >= 0)
        {
            _model.Source = AutoDatePanelModel.Sources[_source.SelectedIndex];
        }
    }

    /// <summary>Picking a format takes the item itself: during the change the edit text may not be updated yet.</summary>
    private void OnFormatSelected(object? sender, EventArgs e)
    {
        if (_format.SelectedIndex >= 0)
        {
            _model.Format = AutoDatePanelModel.CommonFormats[_format.SelectedIndex];
        }
    }

    private void OnPlaceSelected(object? sender, EventArgs e)
    {
        if (_place.SelectedIndex >= 0)
        {
            _model.Placement = AutoDatePanelModel.Placements[_place.SelectedIndex];
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        SelectIfDifferent(_source, AutoDatePanelModel.IndexOf(_model.Source));
        SelectIfDifferent(_place, AutoDatePanelModel.IndexOf(_model.Placement));
        RulePanelLayout.SetTextIfDifferent(_format, _model.Format); // never assign an equal text: it moves the caret
        RulePanelLayout.SetTextIfDifferent(_separator, _model.Separator);

        string error = _model.FormatError;
        RulePanelLayout.Mark(_error, _format, error, Strings.AutoDateFormatHint);
        RulePanelLayout.SetTextIfDifferent(_errorText, error);
        _errorText.Visible = error.Length > 0;

        string example = _model.ExampleText;
        RulePanelLayout.SetTextIfDifferent(_exampleText, example);
        _exampleText.Visible = example.Length > 0;
    }

    private static void SelectIfDifferent(ComboBox box, int index)
    {
        if (box.SelectedIndex != index)
        {
            box.SelectedIndex = index;
        }
    }
}
