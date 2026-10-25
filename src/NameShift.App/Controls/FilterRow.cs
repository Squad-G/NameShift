using System;
using System.Drawing;
using System.Windows.Forms;
using NameShift.App.Style;
using NameShift.Ui;
using NameShift.Ui.Files;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>
/// The filter row above the file list (NS-70): the mask, Files, Folders, Hidden and Sub-folders (NS-78: lists every
/// sub-folder too; the binding reloads the list), and a More toggle that shows a
/// second line with the regex, Ignore case and the lengths; the model's error under them. It renders the
/// <see cref="FilterPanelModel"/> and forwards input: typed text goes through <see cref="FilterTyping"/> (250 ms after
/// the last key), a check box sets the model at once after a flush. The second line has no Alt keys: Alt+O opens it
/// and focuses the regex box. Everything runs on the UI thread.
/// </summary>
internal sealed class FilterRow : UserControl
{
    private const int TextLogicalWidth = 160;
    private const int LengthLogicalWidth = 48;

    private readonly FilterPanelModel _filters;
    private readonly FilterTyping _typing;
    private readonly TextBox _mask = new TextBox();
    private readonly CheckBox _files = new CheckBox();
    private readonly CheckBox _folders = new CheckBox();
    private readonly CheckBox _hidden = new CheckBox();
    private readonly CheckBox _subfolders = new CheckBox();
    private readonly CheckBox _more = new CheckBox();
    private readonly TextBox _regex = new TextBox();
    private readonly CheckBox _ignoreCase = new CheckBox();
    private readonly TextBox _minLength = new TextBox();
    private readonly TextBox _maxLength = new TextBox();
    private readonly FlowLayoutPanel _moreLine = new FlowLayoutPanel();
    private readonly Label _error = new Label();
    private Font _boldFont;
    private bool _syncing;

    public FilterRow(FilterPanelModel filters)
    {
        _filters = filters;
        _typing = new FilterTyping(filters);
        _boldFont = new Font(Font, FontStyle.Bold);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AccessibleName = Strings.FilterRowName;
        AccessibleRole = AccessibleRole.Grouping;
        BuildLayout();
        Sync();

        WireText(_mask, FilterField.Mask);
        WireText(_regex, FilterField.Pattern);
        WireText(_minLength, FilterField.MinLength);
        WireText(_maxLength, FilterField.MaxLength);
        WireCheck(_files, v => _filters.IncludeFiles = v);
        WireCheck(_folders, v => _filters.IncludeFolders = v);
        WireCheck(_hidden, v => _filters.IncludeHidden = v);
        WireCheck(_subfolders, v => _filters.IncludeSubfolders = v);
        WireCheck(_ignoreCase, v => _filters.IgnoreCase = v);
        _more.CheckedChanged += (_, _) => OnMoreToggled();
        _filters.Changed += OnFiltersChanged;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _filters.Changed -= OnFiltersChanged;
            _typing.Dispose();
            _boldFont.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);

        // The bold error font follows the row's font (a DPI change); the old one is disposed once unused.
        Font old = _boldFont;
        _boldFont = new Font(Font, FontStyle.Bold);
        RenderError();
        old.Dispose();
    }

    private void BuildLayout()
    {
        // The first line: the fields in a flow (Sub-folders last, NS-78), a spacer, More at the right.
        var fields = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Margin = Padding.Empty,
            TabIndex = 0,
        };
        AddLabeledBox(fields, Strings.FilterMask, _mask, TextLogicalWidth, true);
        AddCheck(fields, _files, Strings.FilterFiles, true);
        AddCheck(fields, _folders, Strings.FilterFolders, true);
        AddCheck(fields, _hidden, Strings.FilterHidden, true);
        AddCheck(fields, _subfolders, Strings.FilterSubfolders, true);

        _more.Appearance = Appearance.Button;
        _more.AutoSize = true;
        _more.UseMnemonic = true;
        _more.Anchor = AnchorStyles.Right;
        _more.AccessibleName = RulePanelText.PlainTitle(Strings.FilterMore);
        _more.TabIndex = 1;

        var firstLine = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty,
            TabIndex = 0,
        };
        firstLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        firstLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        firstLine.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        firstLine.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        firstLine.Controls.Add(fields, 0, 0);
        firstLine.Controls.Add(_more, 2, 0);

        // The second line, hidden at start; no Alt keys (More opens it and moves the focus to the regex box).
        _moreLine.AutoSize = true;
        _moreLine.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        _moreLine.WrapContents = false;
        _moreLine.Margin = Padding.Empty;
        _moreLine.Visible = false;
        _moreLine.TabIndex = 1;
        AddLabeledBox(_moreLine, Strings.FilterRegex, _regex, TextLogicalWidth, false);
        AddCheck(_moreLine, _ignoreCase, Strings.FilterIgnoreCase, false);
        AddLabeledBox(_moreLine, Strings.FilterMinLength, _minLength, LengthLogicalWidth, false);
        AddLabeledBox(_moreLine, Strings.FilterMaxLength, _maxLength, LengthLogicalWidth, false);

        // The model's error: the words themselves are the cue, in the conflict colour.
        int small = LogicalToDeviceUnits(Spacing.Small);
        _error.AutoSize = true;
        _error.UseMnemonic = false;
        _error.Visible = false;
        _error.Margin = new Padding(small, 0, small, small);
        _error.AccessibleName = Strings.FilterErrorName;
        _error.TabIndex = 2;

        var table = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(firstLine, 0, 0);
        table.Controls.Add(_moreLine, 0, 1);
        table.Controls.Add(_error, 0, 2);
        Controls.Add(table);
    }

    /// <summary>A label and its text box; the label's TabIndex comes just before the box, so its Alt key (when it has
    /// one) focuses the box.</summary>
    private void AddLabeledBox(FlowLayoutPanel line, string labelText, TextBox box, int logicalWidth, bool mnemonic)
    {
        int small = LogicalToDeviceUnits(Spacing.Small);
        var label = new Label
        {
            Text = labelText,
            AutoSize = true,
            UseMnemonic = mnemonic,
            TextAlign = ContentAlignment.MiddleLeft,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(small, 0, 0, 0),
            TabIndex = line.Controls.Count,
        };
        line.Controls.Add(label);

        box.Width = LogicalToDeviceUnits(logicalWidth);
        box.Anchor = AnchorStyles.Left;
        box.Margin = new Padding(small);
        box.AccessibleName = FilterRowText.FieldName(labelText);
        box.TabIndex = line.Controls.Count;
        line.Controls.Add(box);
    }

    private void AddCheck(FlowLayoutPanel line, CheckBox box, string text, bool mnemonic)
    {
        int small = LogicalToDeviceUnits(Spacing.Small);
        box.Text = text;
        box.AutoSize = true;
        box.UseMnemonic = mnemonic;
        box.Anchor = AnchorStyles.Left;
        box.Margin = new Padding(LogicalToDeviceUnits(Spacing.Medium), small, small, small);
        box.AccessibleName = RulePanelText.PlainTitle(text);
        box.TabIndex = line.Controls.Count;
        line.Controls.Add(box);
    }

    private void WireText(TextBox box, FilterField field)
    {
        box.TextChanged += (_, _) =>
        {
            if (!_syncing)
            {
                _typing.Type(field, box.Text);
            }
        };
        box.Leave += (_, _) => _typing.Flush();
        box.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                _typing.Flush();
            }
        };
    }

    private void WireCheck(CheckBox box, Action<bool> set)
    {
        box.CheckedChanged += (_, _) =>
        {
            if (_syncing)
            {
                return;
            }

            // The text typed before the click goes first, so the order the user made is kept.
            _typing.Flush();
            set(box.Checked);
        };
    }

    private void OnMoreToggled()
    {
        _moreLine.Visible = _more.Checked;
        if (_more.Checked)
        {
            _regex.Focus();
        }

        RenderMore();
    }

    private void OnFiltersChanged(object? sender, EventArgs e) => Sync();

    /// <summary>Shows the model: the boxes with no pending text, the check boxes, the More mark and the error. A Reset
    /// clears the boxes; the second line stays as the user left it (view state).</summary>
    private void Sync()
    {
        if (IsDisposed)
        {
            return;
        }

        _syncing = true;
        try
        {
            SyncText(_mask, FilterField.Mask, _filters.Mask);
            SyncText(_regex, FilterField.Pattern, _filters.Pattern);
            SyncText(_minLength, FilterField.MinLength, _filters.MinLengthText);
            SyncText(_maxLength, FilterField.MaxLength, _filters.MaxLengthText);
            RulePanelLayout.SetCheckedIfDifferent(_files, _filters.IncludeFiles);
            RulePanelLayout.SetCheckedIfDifferent(_folders, _filters.IncludeFolders);
            RulePanelLayout.SetCheckedIfDifferent(_hidden, _filters.IncludeHidden);
            RulePanelLayout.SetCheckedIfDifferent(_subfolders, _filters.IncludeSubfolders);
            RulePanelLayout.SetCheckedIfDifferent(_ignoreCase, _filters.IgnoreCase);
        }
        finally
        {
            _syncing = false;
        }

        RenderMore();
        RenderError();
    }

    /// <summary>Never overwrites a box whose text waits to be set: the user's keys are never eaten.</summary>
    private void SyncText(TextBox box, FilterField field, string text)
    {
        if (!_typing.IsPending(field))
        {
            RulePanelLayout.SetTextIfDifferent(box, text);
        }
    }

    private void RenderMore()
    {
        bool shown = _more.Checked;
        _more.Text = FilterRowText.MoreText(_filters, shown);
        _more.AccessibleDescription = FilterRowText.MoreDescription(_filters, shown);
    }

    private void RenderError()
    {
        string? error = _filters.Error;
        _error.Text = error ?? "";
        _error.Visible = error != null;
        _error.ForeColor = Palette.ConflictText;
        _error.Font = SystemInformation.HighContrast ? _boldFont : Font;
    }
}
