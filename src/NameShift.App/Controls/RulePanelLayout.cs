using System;
using System.Windows.Forms;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The layout every rule panel shares: a two-column table of label + field rows and full-width rows.</summary>
internal static class RulePanelLayout
{
    /// <summary>An auto-sized table: an auto-sized label column and a field column taking the rest.</summary>
    public static TableLayoutPanel CreateTable()
    {
        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        return table;
    }

    /// <summary>Adds a label and its field on one row; consecutive tab indexes make the label's mnemonic reach the field.</summary>
    public static void AddTextRow(TableLayoutPanel table, Label label, Control field)
    {
        int row = NewRow(table);
        label.AutoSize = true;
        label.UseMnemonic = true;
        label.Anchor = AnchorStyles.Left;
        label.TabIndex = NextTabIndex(table);
        field.Dock = DockStyle.Fill;
        field.TabIndex = label.TabIndex + 1;
        table.Controls.Add(label, 0, row);
        table.Controls.Add(field, 1, row);
    }

    /// <summary>Adds a labelled field row; the field's accessible name is the label without its mnemonic.</summary>
    /// <returns>The label, for callers that enable or disable it with the field.</returns>
    public static Label AddLabeledRow(TableLayoutPanel table, string labelText, Control field)
    {
        var label = new Label { Text = labelText };
        field.AccessibleName = RulePanelText.PlainTitle(labelText);
        AddTextRow(table, label, field);
        return label;
    }

    /// <summary>Adds a control spanning both columns on its own row.</summary>
    public static void AddSpanningRow(TableLayoutPanel table, Control control)
    {
        int row = NewRow(table);
        control.Anchor = AnchorStyles.Left;
        control.TabIndex = NextTabIndex(table);
        table.Controls.Add(control, 0, row);
        table.SetColumnSpan(control, 2);
    }

    /// <summary>Shows the field's error icon (none when <paramref name="error"/> is empty) and sets its accessible
    /// description to the error, or else to the hint.</summary>
    public static void Mark(ErrorProvider provider, Control field, string error, string? hint)
    {
        provider.SetError(field, error);
        field.AccessibleDescription = error.Length > 0 ? error : hint;
    }

    public static void SetTextIfDifferent(Control control, string text)
    {
        if (control.Text != text)
        {
            control.Text = text;
        }
    }

    public static void SetCheckedIfDifferent(CheckBox box, bool value)
    {
        if (box.Checked != value)
        {
            box.Checked = value;
        }
    }

    private static int NewRow(TableLayoutPanel table)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowCount = table.RowStyles.Count;
        return table.RowCount - 1;
    }

    private static int NextTabIndex(TableLayoutPanel table)
    {
        int next = 0;
        foreach (Control child in table.Controls)
        {
            next = Math.Max(next, child.TabIndex + 1);
        }

        return next;
    }
}
