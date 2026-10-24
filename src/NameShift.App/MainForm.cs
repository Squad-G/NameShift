using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NameShift.App.Controls;
using NameShift.Ui;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;

namespace NameShift.App;

/// <summary>
/// Main window: folder tree (left), file list with live new-name preview (right), rule panels (bottom), and the Reset,
/// Revert and Rename buttons (bottom right). It only composes the controls and wires them to the navigator and the
/// actions; the logic lives in NameShift.Ui. While a Rename or Revert runs, the window is locked and cannot be closed.
/// </summary>
internal sealed class MainForm : Form
{
    private static readonly Size MinimumLogicalSize = new Size(760, 520);
    private static readonly Size InitialLogicalSize = new Size(1100, 720);

    private readonly FolderNavigator _navigator;
    private readonly PathBar _pathBar = new PathBar();
    private readonly FolderTreeModel _folderModel;
    private readonly FolderTree _folderTree;
    private readonly FileList _fileList;
    private readonly SplitContainer _mainSplit = new SplitContainer();
    private readonly SplitContainer _rightSplit = new SplitContainer();
    private readonly ToolStripStatusLabel _statusText = new ToolStripStatusLabel();
    private readonly ToolStripProgressBar _progress = new ToolStripProgressBar { Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly IFolderSource _source;
    private readonly string? _startArgument;
    private readonly string _currentDirectory;
    private readonly OpenSequence _opens = new OpenSequence();
    private (string Folder, string FileName)? _pendingSelection;
    private bool _shownBusy;

    /// <param name="args">The command line: the first path argument opens at start (NS-47).</param>
    /// <param name="currentDirectory">The directory the program started in, for a relative path argument.</param>
    public MainForm(IFolderSource source, IFileListSource fileListSource, IBatchRenamer renamer, string[] args, string currentDirectory)
    {
        _source = source;
        _startArgument = OpenTarget.FirstPathArgument(args);
        _currentDirectory = currentDirectory;
        _navigator = new FolderNavigator(source);
        _folderModel = new FolderTreeModel(source);
        _folderTree = new FolderTree(_folderModel, ShowStatus);
        FileListModel = new FileListModel(fileListSource);

        IReadOnlyList<RulePanelEntry> entries = RulePanels.Create();
        RuleHost = new RulePanelHostModel(entries.Select(e => e.Model));
        PanelHost.Controls.Add(new RulePanelStack(RuleHost, entries) { Dock = DockStyle.Fill });

        Preview = new PreviewModel(RuleHost, FileListModel);
        _fileList = new FileList(FileListModel, Preview, ShowStatus) { Dock = DockStyle.Fill };
        Actions = new MainActions(Preview, FileListModel, RuleHost, renamer);
        ButtonsHost.Controls.Add(new ActionButtons(Actions, ShowReport, ShowStatus));

        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = SystemFonts.MessageBoxFont;
        Text = Strings.AppTitle;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        AllowDropEverywhere(this);
        ApplyWindowSizes();

        _folderTree.FolderSelected += (_, path) => _navigator.SelectFromTree(path);
        _pathBar.PathSubmitted += OnPathSubmitted;
        _navigator.FolderChanged += OnFolderChanged;
        Actions.Changed += (_, _) => OnActionsChanged();
        Actions.FolderContentsChanged += OnFolderContentsChanged;

        // After the file list's own handler (subscribed in its constructor), so the rows exist when one is selected.
        FileListModel.Changed += (_, _) => TryApplyPendingSelection();
    }

    /// <summary>The current folder and its change event.</summary>
    internal FolderNavigator Navigator => _navigator;

    /// <summary>The listed items, for the preview (NS-13) and the selection (NS-14).</summary>
    internal FileListModel FileListModel { get; }

    /// <summary>The enabled rules in pipeline order and their change event, for the preview (NS-13).</summary>
    internal RulePanelHostModel RuleHost { get; }

    /// <summary>The New Name preview, for conflicts (NS-52), selection (NS-14) and Rename (NS-15).</summary>
    internal PreviewModel Preview { get; }

    /// <summary>Rename, Revert and Reset (NS-15).</summary>
    internal MainActions Actions { get; }

    /// <summary>Where the file list docks.</summary>
    internal Panel FileListHost { get; } = new Panel { Dock = DockStyle.Fill };

    /// <summary>Where the rule panel stack docks.</summary>
    internal Panel PanelHost { get; } = new Panel { Dock = DockStyle.Fill };

    /// <summary>Where the Reset, Revert and Rename buttons dock (NS-15).</summary>
    internal FlowLayoutPanel ButtonsHost { get; } = new FlowLayoutPanel
    {
        FlowDirection = FlowDirection.TopDown,
        AutoSize = true,
        WrapContents = false,
        Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
    };

    /// <summary>A non-blocking message in the status bar.</summary>
    internal void ShowStatus(string text)
    {
        _statusText.Text = text;
        _statusText.ToolTipText = text; // the full text, when the bar cuts it
    }

    /// <summary>The result of a Rename or Revert: the status bar, plus a dialog only when items need a look.</summary>
    internal void ShowReport(ActionReport report)
    {
        ShowStatus(report.Status);
        if (report.Details != null)
        {
            MessageBox.Show(this, report.Details, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (Actions.IsBusy && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            ShowStatus(Strings.WaitForAction);
        }

        base.OnFormClosing(e);
    }

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
        if (_startArgument is null)
        {
            _navigator.Start();
        }
        else
        {
            // One navigation once the path is resolved: Documents is never shown first for nothing.
            _ = OpenAsync(_startArgument);
        }

        _ = Actions.LoadJournalAsync();
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
        _pathBar.Margin = new Padding(0, 0, 0, LogicalToDeviceUnits(Spacing.Medium));

        var rootLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(LogicalToDeviceUnits(Spacing.Medium)),
        };
        rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        rootLayout.Controls.Add(_pathBar, 0, 0);
        rootLayout.Controls.Add(_mainSplit, 0, 1);

        _statusText.Spring = true;
        _statusText.TextAlign = ContentAlignment.MiddleLeft;
        var statusStrip = new StatusStrip { Dock = DockStyle.Bottom, ShowItemToolTips = true };
        statusStrip.Items.Add(_statusText);
        statusStrip.Items.Add(_progress);

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

    /// <summary>Locks the window while a Rename or Revert runs, and unlocks it after the folder reloaded.</summary>
    private void OnActionsChanged()
    {
        bool busy = Actions.IsBusy;
        if (busy == _shownBusy || IsDisposed)
        {
            return;
        }

        _shownBusy = busy;
        _mainSplit.Enabled = !busy;
        _pathBar.Enabled = !busy;
        UseWaitCursor = busy;
        _progress.Visible = busy;
        if (busy)
        {
            ShowStatus(Actions.BusyText);
        }
        else
        {
            _fileList.Focus();
        }
    }

    private void OnFolderChanged(object? sender, FolderChangedEventArgs e)
    {
        _opens.Invalidate(); // a pending open from the command line or a drop is now stale
        _pathBar.ShowFolder(e.Path);
        ShowStatus("");
        _ = _folderTree.RevealAsync(e.Path);
        _ = _fileList.ShowFolderAsync(e.Path);
    }

    private async void OnFolderContentsChanged(object? sender, FolderContentsChangedEventArgs e)
    {
        try
        {
            await _folderTree.RefreshAsync(e.Folders);
            if (IsDisposed)
            {
                return;
            }

            // The shown folder is gone when the tree no longer lists it and the disk agrees (the tree leaves out hidden
            // folders). The user may move during the check, so the folder is read again after it.
            string? shown = _navigator.CurrentFolder;
            if (shown is null)
            {
                return;
            }

            string? fallback = await _folderModel.FindFallbackAsync(shown);
            if (IsDisposed || !string.Equals(_navigator.CurrentFolder, shown, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (fallback != null)
            {
                _navigator.SelectFromTree(fallback); // FolderChanged clears the status bar, so say it after.
                ShowStatus(string.Format(Strings.ShownFolderGone, shown));
            }
            else
            {
                // A replaced tree node (a case-only rename) makes the TreeView highlight a sibling: select the shown one again.
                await _folderTree.RevealAsync(shown);
            }
        }
        catch (Exception)
        {
            // An exception escaping an async void handler would end the process.
            ShowStatus(Strings.SomethingWentWrong);
        }
    }

    /// <summary>Opens a path from the command line or a drop. Resolves off the UI thread; never throws.</summary>
    private async Task OpenAsync(string text)
    {
        try
        {
            int ticket = _opens.Begin();
            OpenRequest request = await Task.Run(() => OpenTarget.Resolve(text, _currentDirectory, _source));
            if (IsDisposed)
            {
                return;
            }

            // A late resolve never moves a locked window nor undoes a navigation made meanwhile.
            OpenAction action = _opens.Complete(ticket, request, Actions.IsBusy, _navigator.CurrentFolder != null);
            if (action == OpenAction.Discard)
            {
                return;
            }

            // Folder is never null after Open; the second test is only for the compiler's null analysis.
            if (action != OpenAction.Open || request.Folder is null)
            {
                if (action == OpenAction.OpenStartFolder)
                {
                    _navigator.Start();
                }

                // After the folder change: FolderChanged clears the status bar.
                if (request.Message != null)
                {
                    ShowStatus(request.Message);
                }

                return;
            }

            _pendingSelection = request.FileName is null ? null : (request.Folder, request.FileName);
            string? current = _navigator.CurrentFolder;
            _navigator.SelectFromTree(request.Folder);
            if (current != null && PathInput.SameFolder(current, request.Folder))
            {
                // No FolderChanged for the folder already shown: select now (or once its listing ends).
                TryApplyPendingSelection();
            }
        }
        catch (Exception)
        {
            // Runs fire-and-forget or from an async void handler: an escaping exception would end the process.
            ShowStatus(Strings.SomethingWentWrong);
        }
    }

    /// <summary>Selects the file asked for once its folder is listed; drops the request when the user moved on.</summary>
    private void TryApplyPendingSelection()
    {
        if (_pendingSelection is not { } pending || FileListModel.State == FileListState.Loading)
        {
            return;
        }

        _pendingSelection = null;
        string? listed = FileListModel.FolderPath;
        if (FileListModel.State != FileListState.Loaded || listed is null || !PathInput.SameFolder(listed, pending.Folder))
        {
            return;
        }

        // A file the list does not show (hidden, or removed) leaves nothing selected.
        int row = FileListModel.FindRow(pending.FileName);
        if (row >= 0)
        {
            _fileList.SelectRow(row);
        }
    }

    /// <summary>Every control accepts a dropped folder or file: the tree, the list, the path bar and the panels.</summary>
    private void AllowDropEverywhere(Control control)
    {
        control.AllowDrop = true;
        control.DragEnter += OnDragOver;
        control.DragOver += OnDragOver;
        control.DragDrop += OnDragDrop;
        foreach (Control child in control.Controls)
        {
            AllowDropEverywhere(child);
        }
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        DragDropEffects effect = DragDropEffects.None;
        if (!Actions.IsBusy && e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            if ((e.AllowedEffect & DragDropEffects.Copy) != 0)
            {
                effect = DragDropEffects.Copy;
            }
            else if ((e.AllowedEffect & DragDropEffects.Link) != 0)
            {
                effect = DragDropEffects.Link;
            }
        }

        e.Effect = effect;
    }

    private async void OnDragDrop(object? sender, DragEventArgs e)
    {
        try
        {
            if (Actions.IsBusy || e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)
            {
                return;
            }

            // Only the first item counts. OpenAsync returns at its first await, so Explorer is never held.
            string? first = paths.FirstOrDefault(p => !string.IsNullOrEmpty(p));
            if (first != null)
            {
                await OpenAsync(first);
            }
        }
        catch (Exception)
        {
            // An exception escaping an async void handler would end the process.
            ShowStatus(Strings.SomethingWentWrong);
        }
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
