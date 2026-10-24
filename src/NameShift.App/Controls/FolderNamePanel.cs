using System;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the Append folder name panel: where the folder names go, how many levels and the text between.</summary>
internal sealed class FolderNamePanel : UserControl
{
    private readonly FolderNamePanelModel _model;
    private readonly ComboBox _place = new ComboBox();
    private readonly TextBox _levels = new TextBox();
    private readonly TextBox _separator = new TextBox();
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public FolderNamePanel(FolderNamePanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildLayout();
        Sync();

        _place.SelectedIndexChanged += OnPlaceSelected;
        _levels.TextChanged += (_, _) => _model.LevelsText = _levels.Text;
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

        foreach (var placement in FolderNamePanelModel.Placements)
        {
            _place.Items.Add(FolderNamePlacementText.For(placement));
        }

        _place.DropDownStyle = ComboBoxStyle.DropDownList;

        RulePanelLayout.AddLabeledRow(table, Strings.FolderNamePlace, _place);
        RulePanelLayout.AddLabeledRow(table, Strings.FolderNameLevels, _levels);
        RulePanelLayout.AddLabeledRow(table, Strings.FolderNameSeparator, _separator);

        foreach (Control field in new Control[] { _place, _levels, _separator })
        {
            int small = LogicalToDeviceUnits(Spacing.Small);
            field.Margin = new Padding(small, small, LogicalToDeviceUnits(Spacing.Large + Spacing.Medium), small); // room for the error icon, same right edge for all
            _error.SetIconPadding(field, 2);
        }

        Controls.Add(table);
    }

    private void OnPlaceSelected(object? sender, EventArgs e)
    {
        if (_place.SelectedIndex >= 0)
        {
            _model.Placement = FolderNamePanelModel.Placements[_place.SelectedIndex];
        }
    }

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        SelectIfDifferent(_place, FolderNamePanelModel.IndexOf(_model.Placement));
        RulePanelLayout.SetTextIfDifferent(_levels, _model.LevelsText);
        RulePanelLayout.SetTextIfDifferent(_separator, _model.Separator);

        RulePanelLayout.Mark(_error, _levels, _model.LevelsInvalid ? Strings.FolderNameLevelsInvalid : "", null);
    }

    private static void SelectIfDifferent(ComboBox box, int index)
    {
        if (box.SelectedIndex != index)
        {
            box.SelectedIndex = index;
        }
    }
}
