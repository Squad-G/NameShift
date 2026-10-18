using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using NameShift.App.Controls;
using NameShift.Ui;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;

namespace NameShift.App;

/// <summary>
/// Main window: folder tree (left), file list with live new-name preview (right), rule panels (bottom).
/// It only composes the controls and wires them to the navigator; the logic lives in NameShift.Ui.
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly Size MinimumLogicalSize = new Size(760, 520);
    private static readonly Size InitialLogicalSize = new Size(1100, 720);

    private readonly FolderNavigator _navigator;
    private readonly PathBar _pathBar = new PathBar();
    private readonly FolderTree _folderTree;
    private readonly FileList _fileList;
    private readonly SplitContainer _mainSplit = new SplitContainer();
    private readonly SplitContainer _rightSplit = new SplitContainer();
    private readonly ToolStripStatusLabel _statusText = new ToolStripStatusLabel();

    public MainForm(IFolderSource source, IFileListSource fileListSource)
    {
        _navigator = new FolderNavigator(source);
        _folderTree = new FolderTree(new FolderTreeModel(source), ShowStatus);
        FileListModel = new FileListModel(fileListSource);

        IReadOnlyList<RulePanelEntry> entries = RulePanels.Create();
        RuleHost = new RulePanelHostModel(entries.Select(e => e.Model));
        PanelHost.Controls.Add(new RulePanelStack(RuleHost, entries) { Dock = DockStyle.Fill });

        Preview = new PreviewModel(RuleHost, FileListModel);
        _fileList = new FileList(FileListModel, Preview, ShowStatus) { Dock = DockStyle.Fill };

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = Strings.AppTitle;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        ApplyWindowSizes();

        _folderTree.FolderSelected += (_, path) => _navigator.SelectFromTree(path);
        _pathBar.PathSubmitted += OnPathSubmitted;
        _navigator.FolderChanged += OnFolderChanged;
    }

    /// <summary>The current folder and its change event.</summary>
    internal FolderNavigator Navigator => _navigator;

    /// <summary>The listed items, for the preview (NS-13) and the selection (NS-14).</summary>
    internal FileListModel FileListModel { get; }

    /// <summary>The enabled rules in pipeline order and their change event, for the preview (NS-13).</summary>
    internal RulePanelHostModel RuleHost { get; }

    /// <summary>The New Name preview, for conflicts (NS-52), selection (NS-14) and Rename (NS-15).</summary>
    internal PreviewModel Preview { get; }

    /// <summary>Where the file list docks.</summary>
    internal Panel FileListHost { get; } = new Panel { Dock = DockStyle.Fill };

    /// <summary>Where the rule panel stack docks.</summary>
    internal Panel PanelHost { get; } = new Panel { Dock = DockStyle.Fill };

    /// <summary>Where the action buttons go.</summary>
    internal FlowLayoutPanel ButtonsHost { get; } = new FlowLayoutPanel
    {
        FlowDirection = FlowDirection.TopDown,
        AutoSize = true,
        WrapContents = false,
        Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
    };

    /// <summary>A non-blocking message in the status bar.</summary>
    internal void ShowStatus(string text) => _statusText.Text = text;

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Splitter values are only valid once the containers have their real size.
        _mainSplit.Panel1MinSize = LogicalToDeviceUnits(160);
        _mainSplit.SplitterDistance = LogicalToDeviceUnits(260);
        _rightSplit.Panel1MinSize = LogicalToDeviceUnits(160);
        _rightSplit.Panel2MinSize = LogicalToDeviceUnits(120);
        _rightSplit.SplitterDistance = Math.Max(
            _rightSplit.Panel1MinSize,
            _rightSplit.Height - LogicalToDeviceUnits(240) - _rightSplit.SplitterWidth);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _navigator.Start();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        MinimumSize = LogicalToDeviceUnits(MinimumLogicalSize);
    }

    private void BuildLayout()
    {
        _mainSplit.Dock = DockStyle.Fill;
        _mainSplit.Orientation = Orientation.Vertical;
        _mainSplit.FixedPanel = FixedPanel.Panel1;
        _folderTree.Dock = DockStyle.Fill;
        _mainSplit.Panel1.Controls.Add(_folderTree);

        _rightSplit.Dock = DockStyle.Fill;
        _rightSplit.Orientation = Orientation.Horizontal;
        _rightSplit.FixedPanel = FixedPanel.Panel2;
        FileListHost.Controls.Add(_fileList);
        _rightSplit.Panel1.Controls.Add(FileListHost);
        _rightSplit.Panel2.Controls.Add(BuildBottomLayout());
        _mainSplit.Panel2.Controls.Add(_rightSplit);

        _pathBar.Dock = DockStyle.Fill;
        _pathBar.Margin = new Padding(0, 0, 0, 6);

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(8),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(_pathBar, 0, 0);
        rootLayout.Controls.Add(_mainSplit, 0, 1);

        _statusText.Spring = true;
        _statusText.TextAlign = ContentAlignment.MiddleLeft;
        var statusStrip = new StatusStrip { Dock = DockStyle.Bottom };
        statusStrip.Items.Add(_statusText);

        // Add the Fill control first, then the edge-docked ones: WinForms lays the edge-docked ones out first.
        Controls.Add(rootLayout);
        Controls.Add(statusStrip);
    }

    private TableLayoutPanel BuildBottomLayout()
    {
        var bottom = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        bottom.Controls.Add(PanelHost, 0, 0);
        bottom.Controls.Add(ButtonsHost, 1, 0);
        return bottom;
    }

    private void ApplyWindowSizes()
    {
        MinimumSize = LogicalToDeviceUnits(MinimumLogicalSize);
        Rectangle area = Screen.FromControl(this).WorkingArea;
        Size initial = LogicalToDeviceUnits(InitialLogicalSize);
        Size = new Size(Math.Min(initial.Width, area.Width), Math.Min(initial.Height, area.Height));
    }

    private void OnFolderChanged(object? sender, FolderChangedEventArgs e)
    {
        _pathBar.ShowFolder(e.Path);
        ShowStatus("");
        _ = _folderTree.RevealAsync(e.Path);
        _ = _fileList.ShowFolderAsync(e.Path);
    }

    private async void OnPathSubmitted(object? sender, string text)
    {
        try
        {
            PathBarResult result = await _navigator.NavigateToTypedPathAsync(text);
            ShowPathResult(result, text);
        }
        catch (Exception)
        {
            // An exception escaping an async void handler would end the process.
            ShowStatus(Strings.SomethingWentWrong);
        }
    }

    private void ShowPathResult(PathBarResult result, string text)
    {
        switch (result)
        {
            case PathBarResult.Navigated:
                ShowStatus("");
                break;
            case PathBarResult.Unchanged:
                ShowStatus("");
                _pathBar.ShowFolder(_navigator.CurrentFolder ?? "");
                break;
            case PathBarResult.Empty:
                _pathBar.ShowFolder(_navigator.CurrentFolder ?? "");
                break;
            case PathBarResult.Invalid:
                ShowStatus(Strings.PathInvalid);
                _pathBar.SelectAllText();
                break;
            case PathBarResult.NotFound:
                ShowStatus(string.Format(Strings.PathNotFound, text));
                _pathBar.SelectAllText();
                break;
            case PathBarResult.NotAFolder:
                ShowStatus(string.Format(Strings.PathNotAFolder, text));
                _pathBar.SelectAllText();
                break;
        }
    }
}
