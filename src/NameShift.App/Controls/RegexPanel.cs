using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The content of the RegEx panel: the pattern with its error under the box, the replacement, Ignore case and Include extension.</summary>
internal sealed class RegexPanel : UserControl
{
    private const int ErrorLogicalWidth = 220;

    private readonly RegexPanelModel _model;
    private readonly TextBox _pattern = new TextBox();
    private readonly TextBox _replacement = new TextBox();
    private readonly CheckBox _ignoreCase = new CheckBox();
    private readonly CheckBox _includeExtension = new CheckBox();
    private readonly Label _errorText = new Label();
    private readonly ErrorProvider _error = new ErrorProvider { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    public RegexPanel(RegexPanelModel model)
    {
        _model = model;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        BuildLayout();
        Sync();

        _pattern.TextChanged += (_, _) => _model.Pattern = _pattern.Text;
        _replacement.TextChanged += (_, _) => _model.Replacement = _replacement.Text;
        _ignoreCase.CheckedChanged += (_, _) => _model.IgnoreCase = _ignoreCase.Checked;
        _includeExtension.CheckedChanged += (_, _) => _model.IncludeExtension = _includeExtension.Checked;
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

        RulePanelLayout.AddLabeledRow(table, Strings.RegexPattern, _pattern);

        _errorText.AutoSize = true;
        _errorText.UseMnemonic = false;
        _errorText.Visible = false;
        ApplyErrorWidth();
        RulePanelLayout.AddSpanningRow(table, _errorText);

        RulePanelLayout.AddLabeledRow(table, Strings.RegexReplacement, _replacement);

        foreach (Control field in new Control[] { _pattern, _replacement })
        {
            int small = LogicalToDeviceUnits(Spacing.Small);
            field.Margin = new Padding(small, small, LogicalToDeviceUnits(Spacing.Large + Spacing.Medium), small); // room for the error icon, same right edge for both
        }

        _error.SetIconPadding(_pattern, 2);

        _ignoreCase.Text = Strings.RegexIgnoreCase;
        _ignoreCase.AutoSize = true;
        RulePanelLayout.AddSpanningRow(table, _ignoreCase);
        _includeExtension.Text = Strings.RegexIncludeExtension;
        _includeExtension.AutoSize = true;
        RulePanelLayout.AddSpanningRow(table, _includeExtension);
        Controls.Add(table);
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        ApplyErrorWidth();
    }

    /// <summary>Lets the error text wrap within the width of the other panels.</summary>
    private void ApplyErrorWidth() => _errorText.MaximumSize = new Size(LogicalToDeviceUnits(ErrorLogicalWidth), 0);

    private void OnModelChanged(object? sender, EventArgs e) => Sync();

    private void Sync()
    {
        RulePanelLayout.SetTextIfDifferent(_pattern, _model.Pattern);
        RulePanelLayout.SetTextIfDifferent(_replacement, _model.Replacement);
        RulePanelLayout.SetCheckedIfDifferent(_ignoreCase, _model.IgnoreCase);
        RulePanelLayout.SetCheckedIfDifferent(_includeExtension, _model.IncludeExtension);

        string error = _model.PatternError;
        RulePanelLayout.Mark(_error, _pattern, error, Strings.RegexPatternHint);
        RulePanelLayout.SetTextIfDifferent(_errorText, error);
        _errorText.Visible = error.Length > 0;
    }
}
