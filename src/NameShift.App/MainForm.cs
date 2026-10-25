using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NameShift.App.Controls;
using NameShift.App.Dialogs;
using NameShift.Ui;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using NameShift.Ui.Presets;
using NameShift.Ui.Preview;
using NameShift.Ui.Rules;
using NameShift.Ui.Settings;
using MainMenu = NameShift.App.Controls.MainMenu;

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
    private readonly MainMenu _menu;
    private readonly SplitContainer _mainSplit = new SplitContainer();
    private readonly SplitContainer _rightSplit = new SplitContainer();
    private readonly ToolStripStatusLabel _statusText = new ToolStripStatusLabel();
    private readonly ToolStripProgressBar _progress = new ToolStripProgressBar { Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly IFolderSource _source;
    private readonly string? _startArgument;
    private readonly IReadOnlyList<string> _startArguments;
    private readonly string _currentDirectory;
    private readonly OpenSequence _opens = new OpenSequence();
    private readonly FilterBinding _filterBinding;
    private readonly SkippedFoldersNotice _skippedFolders;
    private readonly RulePanelStack _stack;
    private bool _fitting;
    private (string Folder, IReadOnlyList<string> Names, int Rejected, bool Report)? _pendingSelection;
    private bool _shownBusy;
    private readonly AppSettingsStore _settingsStore;
    private readonly AppSettings _saved;
    private PlacedWindow? _placed;
    private bool _maximizedBeforeMinimize;

    /// <param name="args">The command line: the first path argument opens at start (NS-47).</param>
    /// <param name="currentDirectory">The directory the program started in, for a relative path argument.</param>
    /// <param name="settingsStore">Where the window facts and the last rules are saved at close (NS-37).</param>
    /// <param name="saved">The window facts read at start (NS-37).</param>
    /// <param name="rules">The last rules read at start, or null (NS-37).</param>
    public MainForm(
        IFolderSource source,
        IFileListSource fileListSource,
        IBatchRenamer renamer,
        string[] args,
        string currentDirectory,
        PresetStore presetStore,
        AppSettingsStore settingsStore,
        AppSettings saved,
        Preset? rules)
    {
        _source = source;
        _settingsStore = settingsStore;
        _saved = saved;
        _startArgument = OpenTarget.FirstPathArgument(args);
        _startArguments = OpenTarget.PathArguments(args);
        _currentDirectory = currentDirectory;
        _navigator = new FolderNavigator(source);
        _folderModel = new FolderTreeModel(source);
        _folderTree = new FolderTree(_folderModel, ShowStatus);
        FileListModel = new FileListModel(fileListSource);

        IReadOnlyList<RulePanelEntry> entries = RulePanels.Create();
        RuleHost = new RulePanelHostModel(entries.Select(e => e.Model));

        // The last rules come back before the preview, the actions and any listing (NS-37), and before the collapse
        // model, so the restored ticked panels start open.
        if (rules != null)
        {
            RuleHost.ApplyPreset(rules);
        }

        PanelCollapse = new PanelCollapseModel(RuleHost, _saved.CollapsedPanels);
        _stack = new RulePanelStack(RuleHost, entries, PanelCollapse) { Dock = DockStyle.Fill };
        _stack.LayoutNeeded += (_, _) => FitBottomArea();
        PanelHost.Controls.Add(_stack);

        Preview = new PreviewModel(RuleHost, FileListModel);
        _fileList = new FileList(FileListModel, Preview, ShowStatus) { Dock = DockStyle.Fill };
        Filters = new FilterPanelModel();

        // The Folder column follows the Sub-folders box, not the listing: it shows at once, even while loading (NS-78).
        Filters.Changed += (_, _) => _fileList.ShowFolderColumn(Filters.IncludeSubfolders);
        _filterBinding = new FilterBinding(Filters, FileListModel);
        _skippedFolders = new SkippedFoldersNotice(FileListModel, CultureInfo.CurrentCulture);
        _skippedFolders.Notice += (_, text) => ShowStatus(text);
        _skippedFolders.Withdrawn += (_, text) => ClearStatusIf(text);
        FilterHost.Controls.Add(new FilterRow(Filters) { Dock = DockStyle.Fill });
        Actions = new MainActions(Preview, FileListModel, RuleHost, Filters, renamer);
        ButtonsHost.Controls.Add(new ActionButtons(Actions, ShowReport, ShowStatus, AskYesNo, check => RevertCheckDialog.Ask(this, check)));
        var presets = new PresetMenuModel(presetStore, RuleHost, () => Actions.IsBusy);
        _menu = new MainMenu(presets, AskYesNo, preFill => PresetNameDialog.Ask(this, preFill), ShowStatus, () => IsDisposed)
        {
            Dock = DockStyle.Top,
        };

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
        _fileList.FolderOpenRequested += OnFolderOpenRequested;
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

    /// <summary>Which rule panels are collapsed to their title line (NS-83): in memory only, unticked panels start
    /// collapsed. The bottom area's height follows it.</summary>
    internal PanelCollapseModel PanelCollapse { get; }

    /// <summary>The New Name preview, for conflicts (NS-52), selection (NS-14) and Rename (NS-15).</summary>
    internal PreviewModel Preview { get; }

    /// <summary>Rename, Revert and Reset (NS-15).</summary>
    internal MainActions Actions { get; }

    /// <summary>The filter row's settings (NS-70): created once, at default, never saved; they stay when another folder
    /// opens. Reset clears them with the rules.</summary>
    internal FilterPanelModel Filters { get; }

    /// <summary>Where the filter row docks (NS-70), above the file list.</summary>
    internal Panel FilterHost { get; } = new Panel { Dock = DockStyle.Fill, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

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

    /// <summary>Clears the status bar only while it still shows this text (a newer message stays).</summary>
    private void ClearStatusIf(string text)
    {
        if (_statusText.Text == text)
        {
            ShowStatus("");
        }
    }

    /// <summary>A Yes/No question; Escape is No. The default button is Yes only when asked to.</summary>
    internal bool AskYesNo(string question, bool yesIsDefault) => YesNoDialog.Ask(this, question, yesIsDefault);

    /// <summary>The result of a Rename or Revert: the status bar, plus a dialog only when items need a look.</summary>
    internal void ShowReport(ActionReport report)
    {
        ShowStatus(report.Status);
        if (report.Details != null)
        {
            MessageBox.Show(this, report.Details, Strings.AppTitle, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Before the controls go: a filter run or a load still in flight never raises an event on a dead window.
            // The filter row disposes its own typing delay when the base disposes the controls.
            _filterBinding.Dispose();
            _skippedFolders.Dispose();
            FileListModel.Dispose();
            PanelCollapse.Dispose();
        }

        base.Dispose(disposing);
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

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        // Only reached when the close was not cancelled: a close refused while a Rename or Revert runs saves nothing.
        SaveSettings();
        base.OnFormClosed(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState != FormWindowState.Minimized)
        {
            _maximizedBeforeMinimize = WindowState == FormWindowState.Maximized;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyLandingScale();

        // Splitter values are only valid once the containers have their real size.
        _mainSplit.Panel1MinSize = LogicalToDeviceUnits(160);
        _mainSplit.SplitterDistance = SplitterRule.Clamp(
            _saved.MainSplit is int m ? LogicalToDeviceUnits(m) : null,
            LogicalToDeviceUnits(260),
            _mainSplit.Width,
            _mainSplit.Panel1MinSize,
            _mainSplit.Width - _mainSplit.Panel2MinSize - _mainSplit.SplitterWidth);
        _rightSplit.Panel1MinSize = LogicalToDeviceUnits(160);

        // The bottom area's height follows the panels (NS-83): set in code only, never dragged.
        _rightSplit.IsSplitterFixed = true;
        _stack.StartCollapse();
        FitBottomArea();
        _rightSplit.SizeChanged += (_, _) => FitBottomArea();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        switch (StartFolderChoice.Pick(_startArgument, _saved.LastFolder))
        {
            case StartFolderSource.Argument:
                // One navigation once the path is resolved: Documents is never shown first for nothing.
                _ = _startArguments.Count >= 2 ? OpenManyAsync(_startArguments) : OpenAsync(_startArgument!);
                break;
            case StartFolderSource.Remembered:
                _ = OpenRememberedAsync(_saved.LastFolder!);
                break;
            default:
                _navigator.Start();
                break;
        }

        _ = Actions.LoadJournalAsync();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        MinimumSize = LogicalToDeviceUnits(MinimumLogicalSize);
        if (IsHandleCreated)
        {
            BeginInvoke(new Action(FitBottomArea)); // once the controls have rescaled
        }
    }

    /// <summary>
    /// Sizes the bottom area (rule panels and buttons) to the wrapped panels (NS-83): never below the buttons, never
    /// above what leaves the file list its minimum (the panels scroll there). Ends: moving the splitter of a horizontal
    /// split changes only the area's height, never its width, so measuring again gives the same answer; the flag stops
    /// the synchronous re-entry.
    /// </summary>
    private void FitBottomArea()
    {
        if (_fitting || !IsHandleCreated || IsDisposed || WindowState == FormWindowState.Minimized)
        {
            return;
        }

        _fitting = true;
        try
        {
            int splitter = _rightSplit.SplitterWidth;
            int lowest = _rightSplit.Panel1MinSize;
            int highest = _rightSplit.Height - _rightSplit.Panel2MinSize - splitter;
            if (highest < lowest)
            {
                return; // too small to place the splitter
            }

            // Heights of the bottom area (the split's lower panel). The stack sits inside PanelHost's cell margin, so its
            // height is the area's minus that margin. This is the only place the minimum and the cap are applied.
            int hostMargin = PanelHost.Margin.Vertical;
            int minimum = ButtonsHost.PreferredSize.Height + ButtonsHost.Margin.Vertical;
            int cap = Math.Max(minimum, _rightSplit.Height - lowest - splitter);
            int panels = _stack.PreferredAreaHeight(Math.Max(0, cap - hostMargin)) + hostMargin;
            int height = Math.Min(cap, Math.Max(minimum, panels));
            int distance = Math.Min(highest, Math.Max(lowest, _rightSplit.Height - height - splitter));
            if (distance != _rightSplit.SplitterDistance)
            {
                _rightSplit.SplitterDistance = distance;
            }
        }
        finally
        {
            _fitting = false;
        }
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

        // The filter row above the list, added first so the Tab order is filter row, then file list (NS-70).
        var listLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
        };
        listLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        listLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        FilterHost.Margin = Padding.Empty;
        FileListHost.Margin = Padding.Empty;
        listLayout.Controls.Add(FilterHost, 0, 0);
        listLayout.Controls.Add(FileListHost, 0, 1);
        _rightSplit.Panel1.Controls.Add(listLayout);
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
        Controls.Add(_menu);
        MainMenuStrip = _menu;
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

        // The saved placement when it still lands on a screen (NS-37); otherwise centred at the default size.
        _placed = _saved.Window == null
            ? null
            : WindowPlacement.Fit(_saved.Window, ScreenAreas.Current(), MinimumLogicalSize.Width, MinimumLogicalSize.Height);
        if (_placed != null)
        {
            StartPosition = FormStartPosition.Manual;
            Bounds = new Rectangle(_placed.Left, _placed.Top, _placed.Width, _placed.Height);
            if (_placed.Maximized)
            {
                // After the bounds, so they become the restore bounds. Never minimised.
                WindowState = FormWindowState.Maximized;
            }

            return;
        }

        Rectangle area = Screen.FromControl(this).WorkingArea;
        Size initial = LogicalToDeviceUnits(InitialLogicalSize);
        Size = new Size(Math.Min(initial.Width, area.Width), Math.Min(initial.Height, area.Height));
    }

    /// <summary>
    /// Once the handle exists, <see cref="Control.DeviceDpi"/> is the landing screen's. When the window's size is not
    /// the saved logical size at that scale (the screen's scale was not the expected one, or WinForms rescaled the
    /// window as it was created), the size is set again from the saved logical size (NS-37 AC 7). For a maximised
    /// window this sets the restore size underneath.
    /// </summary>
    private void ApplyLandingScale()
    {
        if (_placed == null || _saved.Window == null)
        {
            return;
        }

        try
        {
            MinimumSize = LogicalToDeviceUnits(MinimumLogicalSize);
            Rectangle area = Screen.FromControl(this).WorkingArea;
            var landing = new ScreenArea(area.Left, area.Top, area.Width, area.Height, DeviceDpi / 96.0);
            var saved = new SavedWindow(_placed.Left, _placed.Top, _saved.Window.Width, _saved.Window.Height, _placed.Maximized);
            PlacedWindow? again = WindowPlacement.Fit(saved, new[] { landing }, MinimumLogicalSize.Width, MinimumLogicalSize.Height);
            if (again == null)
            {
                return;
            }

            var target = new Size(again.Width, again.Height);
            Size current = WindowState == FormWindowState.Normal ? Size : RestoreBounds.Size;
            if (current != target)
            {
                Size = target;
            }
        }
        catch (Exception)
        {
            // A placement detail must never stop the window from opening.
        }
    }

    /// <summary>Saves the window facts and the rules (NS-37) at close: on the pool, waiting at most 2 seconds, silent.
    /// Nothing of the filters, the sort, the selection, the sub-folders box, the collapse state or the lock is read.</summary>
    private void SaveSettings()
    {
        try
        {
            Rectangle b = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            bool maximized = WindowState == FormWindowState.Maximized
                || (WindowState == FormWindowState.Minimized && _maximizedBeforeMinimize);
            double scale = DeviceDpi / 96.0;
            SavedWindow? window = b.Width > 0 && b.Height > 0
                ? WindowPlacement.ToSaved(b.Left, b.Top, b.Width, b.Height, scale, maximized)
                : null;
            int split = (int)Math.Round(_mainSplit.SplitterDistance / scale, MidpointRounding.AwayFromZero);

            // A start that never showed a folder keeps the one read at start instead of erasing it.
            var settings = new AppSettings(
                window, split >= 0 ? split : null, _navigator.CurrentFolder ?? _saved.LastFolder, PanelCollapse.States);
            _settingsStore.SaveWithin(
                settings, Preset.FromPanels(AppSettingsStore.LastRulesName, RuleHost.Panels), AppSettingsStore.SaveLimit);
        }
        catch (Exception)
        {
            // A close must never crash, and a failure to save is silent.
        }
    }

    /// <summary>Opens the folder shown at the last close (NS-37 AC 8): checked off the UI thread; a folder that is gone
    /// falls back to the start folder with no message. A click in the tree or a typed path meanwhile wins (its
    /// FolderChanged invalidates the ticket). Never throws.</summary>
    private async Task OpenRememberedAsync(string folder)
    {
        try
        {
            int ticket = _opens.Begin();
            OpenRequest request = await Task.Run(() => StartFolderChoice.Resolve(folder, _source));
            if (IsDisposed)
            {
                return;
            }

            switch (_opens.Complete(ticket, request, Actions.IsBusy, _navigator.CurrentFolder != null))
            {
                case OpenAction.Open when request.Folder != null:
                    _navigator.SelectFromTree(request.Folder);
                    break;
                case OpenAction.OpenStartFolder:
                    _navigator.Start();
                    break;
            }
        }
        catch (Exception)
        {
            // Runs fire-and-forget: an escaping exception would end the process.
            ShowStatus(Strings.SomethingWentWrong);
        }
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
        _menu.Enabled = !busy;
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

            _pendingSelection = request.FileName is null ? null : (request.Folder, new[] { request.FileName }, 0, false);
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

    /// <summary>Opens several paths from the command line: the folder of the first that exists, with those items selected
    /// (NS-88). Resolves off the UI thread; never throws.</summary>
    private async Task OpenManyAsync(IReadOnlyList<string> texts)
    {
        try
        {
            int ticket = _opens.Begin();
            string currentDirectory = _currentDirectory;
            IFolderSource source = _source;
            MultiOpenRequest many = await Task.Run(() =>
                OpenTarget.ResolveMany(texts.Select(t => new PathArgument(t, currentDirectory)).ToList(), source));
            if (IsDisposed)
            {
                return;
            }

            OpenRequest request = many.Request;
            OpenAction action = _opens.Complete(ticket, request, Actions.IsBusy, _navigator.CurrentFolder != null);
            if (action == OpenAction.Discard)
            {
                return;
            }

            if (action != OpenAction.Open || many.Folder is null)
            {
                if (action == OpenAction.OpenStartFolder)
                {
                    _navigator.Start();
                }

                if (many.Message != null)
                {
                    ShowStatus(many.Message);
                }

                return;
            }

            _pendingSelection = (many.Folder, many.Names, many.Rejected, true);
            string? current = _navigator.CurrentFolder;
            _navigator.SelectFromTree(many.Folder);
            if (current != null && PathInput.SameFolder(current, many.Folder))
            {
                TryApplyPendingSelection();
            }
        }
        catch (Exception)
        {
            ShowStatus(Strings.SomethingWentWrong);
        }
    }

    /// <summary>Selects the items asked for once their folder is listed; drops the request when the user moved on.</summary>
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

        // A name the list does not show (hidden, filtered out, or removed) is not selected; none found leaves the
        // selection as it is. One file from the command line or a drop is silent, as in NS-47.
        IReadOnlyList<int> rows = FileListModel.SelectNames(pending.Names);
        _fileList.SelectRows(rows);
        int notSelected = pending.Rejected + (pending.Names.Count - rows.Count);
        if (pending.Report && notSelected > 0)
        {
            ShowStatus(
                notSelected == 1
                    ? string.Format(CultureInfo.CurrentCulture, Strings.NotSelectedOne, pending.Folder)
                    : string.Format(CultureInfo.CurrentCulture, Strings.NotSelectedMany, notSelected, pending.Folder));
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

    private async void OnFolderOpenRequested(object? sender, string path)
    {
        try
        {
            PathBarResult result = await _navigator.NavigateToFolderAsync(path);
            string? text = FolderOpenText.For(result, path, CultureInfo.CurrentCulture);
            if (text != null)
            {
                ShowStatus(text);
            }
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
