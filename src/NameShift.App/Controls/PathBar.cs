using System;
using System.Windows.Forms;
using NameShift.Ui;

namespace NameShift.App.Controls;

/// <summary>The folder path box: Enter submits, Escape or leaving the box puts the shown folder back.</summary>
internal sealed class PathBar : UserControl
{
    private readonly TextBox _box = new TextBox();
    private string _shownPath = "";

    public PathBar()
    {
        BuildLayout();
    }

    /// <summary>Raised on Enter with the typed text.</summary>
    public event EventHandler<string>? PathSubmitted;

    /// <summary>Shows the current folder's path.</summary>
    public void ShowFolder(string path)
    {
        _shownPath = path;
        _box.Text = path;
    }

    public void SelectAllText()
    {
        _box.Focus();
        _box.SelectAll();
    }

    private void BuildLayout()
    {
        var label = new Label
        {
            Text = Strings.PathBarLabel,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 0, 8, 0),
        };
        _box.Dock = DockStyle.Fill;
        _box.AccessibleName = Strings.PathBarLabel;
        _box.AccessibleDescription = Strings.PathBarHint;
        _box.KeyDown += OnBoxKeyDown;
        _box.Leave += (_, _) => _box.Text = _shownPath;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(label, 0, 0);
        layout.Controls.Add(_box, 1, 0);

        AutoSize = true;
        Controls.Add(layout);
    }

    private void OnBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            PathSubmitted?.Invoke(this, _box.Text);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.SuppressKeyPress = true;
            _box.Text = _shownPath;
            _box.SelectAll();
        }
    }
}
