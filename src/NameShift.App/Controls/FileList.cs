using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NameShift.App.Style;
using NameShift.Core;
using NameShift.Ui;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;

namespace NameShift.App.Controls;

/// <summary>Renders the file list model in a virtual-mode list: only the visible rows are ever built.</summary>
internal sealed class FileList : UserControl
{
    /// <summary>Raised on the UI thread with the full path of a folder row to open (double-click or Enter).</summary>
    internal event EventHandler<string>? FolderOpenRequested;

    private const int FolderImage = 0;
    private const int FileImage = 1;

    private readonly FileListModel _model;
    private readonly PreviewModel _preview;
    private readonly Action<string> _showStatus;
    private readonly ListView _list = new BufferedListView();
    private readonly Label _message = new Label();
    private readonly Label _summary = new Label();
    private readonly Label _count = new Label();
    private readonly Panel _strip = new Panel();
    private readonly ImageList _icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<Bitmap> _iconBitmaps = new List<Bitmap>();

    // One header per column, kept while hidden, so a width the user dragged survives a hide and a show (NS-78).
    private readonly Dictionary<ShownColumn, ColumnHeader> _headers = new Dictionary<ShownColumn, ColumnHeader>();

    // Every column index (the row's cells, the sort click, the sort arrow) comes from this map, never a constant.
    private FileListColumns _columns = FileListColumns.For(false);
    private IReadOnlyList<RenameItem>? _shownItems;
    private bool _selectionReadPending;
    private int _shownCount = -1;
    private Font _boldFont;

    public FileList(FileListModel model, PreviewModel preview, Action<string> showStatus)
    {
        _model = model;
        _preview = preview;
        _showStatus = showStatus;
        _boldFont = new Font(_list.Font, FontStyle.Bold);
        BuildLayout();
        _model.Changed += (_, _) => Render();
        _model.SelectionChanged += (_, _) => RenderCount();
        _model.OrderChanged += (_, _) => RenderOrder();
        _preview.Changed += (_, _) => RenderPreview();
    }

    /// <summary>Lists the folder; fire-and-forget from the window, so a failure goes to the status bar.</summary>
    public async Task ShowFolderAsync(string path)
    {
        try
        {
            await _model.LoadAsync(path);
        }
        catch (Exception)
        {
            // Runs fire-and-forget: report instead of letting the exception go unobserved.
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    /// <summary>Shows or hides the Folder column right after Name (NS-78: bound to the Sub-folders box, so it shows at
    /// once, even while the folder loads). The other headers are never touched, so their widths stay. The headers and
    /// the column map change inside one update, so no row is built with the wrong number of cells.</summary>
    internal void ShowFolderColumn(bool show)
    {
        if (IsDisposed || show == _columns.ShowFolder)
        {
            return;
        }

        FileListColumns next = FileListColumns.For(show);
        ColumnHeader folder = _headers[ShownColumn.Folder];
        _list.BeginUpdate();
        try
        {
            if (show)
            {
                _list.Columns.Insert(next.IndexOf(ShownColumn.Folder), folder);
            }
            else
            {
                // A removed header reads its width from the column that takes its index: keep the user's own.
                int width = folder.Width;
                _list.Columns.Remove(folder);
                folder.Width = width;
            }

            _columns = next;
        }
        finally
        {
            _list.EndUpdate();
        }

        _list.Invalidate();
        UpdateSortArrow();
    }

    /// <summary>Selects only this row, focuses it and scrolls it into view; a row outside the list is ignored.</summary>
    public void SelectRow(int row)
    {
        if (IsDisposed || !_list.IsHandleCreated || row < 0 || row >= _list.VirtualListSize)
        {
            return;
        }

        SetAllRowsSelected(false);
        _list.SelectedIndices.Add(row);
        _list.FocusedItem = _list.Items[row];
        _list.EnsureVisible(row);

        // The list's selection events schedule this too; one read updates the model and the "1 of M" count.
        ScheduleSelectionRead();
    }

    /// <summary>Selects only these rows (one message per row), focuses and scrolls to the first; a row outside the list is
    /// ignored.</summary>
    public void SelectRows(IReadOnlyList<int> rows)
    {
        if (IsDisposed || !_list.IsHandleCreated || rows.Count == 0)
        {
            return;
        }

        SetAllRowsSelected(false);
        int first = -1;
        foreach (int row in rows)
        {
            if (row < 0 || row >= _list.VirtualListSize)
            {
                continue;
            }

            SetRowSelected(row);
            if (first < 0)
            {
                first = row;
            }
        }

        if (first >= 0)
        {
            _list.FocusedItem = _list.Items[first];
            _list.EnsureVisible(first);
        }

        // The list's selection events schedule this too; one read updates the model and the "N of M" count.
        ScheduleSelectionRead();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boldFont.Dispose();
            _icons.Dispose();

            // A hidden header is not in the list, so the list does not dispose it.
            foreach (ColumnHeader header in _headers.Values)
            {
                if (header.ListView == null)
                {
                    header.Dispose();
                }
            }
            foreach (Bitmap bitmap in _iconBitmaps)
            {
                bitmap.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    private void BuildLayout()
    {
        _list.Dock = DockStyle.Fill;
        _list.View = View.Details;
        _list.VirtualMode = true;
        _list.MultiSelect = true;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.HeaderStyle = ColumnHeaderStyle.Clickable;
        _list.AccessibleName = Strings.FileListName;
        _list.HandleCreated += (_, _) =>
        {
            SetWindowTheme(_list.Handle, "explorer", null);
            UpdateSortArrow();
        };
        _list.ColumnClick += OnColumnClick;
        _list.RetrieveVirtualItem += (_, e) => e.Item = BuildRow(e.ItemIndex);
        _list.FontChanged += (_, _) => RefreshBoldFont();
        _list.SelectedIndexChanged += (_, _) => ScheduleSelectionRead();
        _list.VirtualItemsSelectionRangeChanged += (_, _) => ScheduleSelectionRead();
        _list.KeyDown += OnListKeyDown;
        _list.MouseDoubleClick += OnListDoubleClick;

        // Every header is created once; only the columns of the current map are added (Folder is not, at first).
        foreach (ShownColumn column in FileListColumns.For(true).Order)
        {
            _headers[column] = CreateHeader(column);
        }

        foreach (ShownColumn column in _columns.Order)
        {
            _list.Columns.Add(_headers[column]);
        }

        _icons.ImageSize = new Size(LogicalToDeviceUnits(16), LogicalToDeviceUnits(16));
        AddStockIcon(StockIcons.Folder);
        AddStockIcon(StockIcons.File);
        _list.SmallImageList = _icons;

        _message.ForeColor = SystemColors.GrayText;
        _message.BackColor = _list.BackColor;
        _message.TextAlign = ContentAlignment.TopCenter;
        _message.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _message.Location = new Point(0, LogicalToDeviceUnits(32));
        _message.Size = new Size(_list.ClientSize.Width, LogicalToDeviceUnits(24));
        _message.Visible = false;
        _list.Controls.Add(_message);

        _count.Dock = DockStyle.Left;
        _count.AutoSize = true;
        _count.TextAlign = ContentAlignment.MiddleLeft;
        _count.ForeColor = SystemColors.WindowText;
        _count.Padding = new Padding(LogicalToDeviceUnits(Spacing.Small), 0, 0, 0);

        _summary.Dock = DockStyle.Fill;
        _summary.AutoSize = false;
        _summary.TextAlign = ContentAlignment.MiddleLeft;
        _summary.Padding = new Padding(LogicalToDeviceUnits(Spacing.Small), 0, 0, 0);

        // Fill first, then Left: docking runs back to front, so the count takes its width and the summary the rest.
        _strip.Dock = DockStyle.Bottom;
        _strip.Height = LogicalToDeviceUnits(22);
        _strip.Visible = false;
        _strip.Controls.Add(_summary);
        _strip.Controls.Add(_count);

        // Fill first, then Bottom: docking runs back to front, so the strip takes its row and the list the rest.
        Controls.Add(_list);
        Controls.Add(_strip);
    }

    private ColumnHeader CreateHeader(ShownColumn column)
    {
        (string text, int width) = column switch
        {
            ShownColumn.Name => (Strings.ColumnName, 280),
            ShownColumn.Folder => (Strings.ColumnFolder, 160),
            ShownColumn.NewName => (Strings.ColumnNewName, 280),
            ShownColumn.Status => (Strings.ColumnStatus, 420),
            ShownColumn.Size => (Strings.ColumnSize, 90),
            _ => (Strings.ColumnModified, 150),
        };
        return new ColumnHeader
        {
            Text = text,
            Width = LogicalToDeviceUnits(width),
            TextAlign = column == ShownColumn.Size ? HorizontalAlignment.Right : HorizontalAlignment.Left,
        };
    }

    private void AddStockIcon(int stockIconId)
    {
        // A failed shell call leaves a blank image, so the folder and file indexes stay put.
        Bitmap bitmap = StockIcons.Load(stockIconId, _icons.ImageSize)
            ?? new Bitmap(_icons.ImageSize.Width, _icons.ImageSize.Height);
        // Images.Add(Image) only keeps a reference, not ownership: the control keeps the bitmap and disposes it.
        _iconBitmaps.Add(bitmap);
        _icons.Images.Add(bitmap);
    }

    private void Render()
    {
        if (IsDisposed)
        {
            return;
        }

        int count = _model.Items.Count;
        if (!ReferenceEquals(_shownItems, _model.Items))
        {
            // A new list never keeps the old rows selected: the model has reset its selection too.
            if (_shownCount > 0 && _list.IsHandleCreated)
            {
                SetAllRowsSelected(false);
            }

            _shownItems = _model.Items;
        }

        _list.VirtualListSize = count;
        _list.Invalidate();
        if (count != _shownCount && count > 0)
        {
            // A new folder never opens mid-list.
            _list.EnsureVisible(0);
        }

        _shownCount = count;

        string? text = _model.Message;
        _message.Text = text ?? "";
        _message.Visible = text != null;
        RenderCount();
        RenderSummary();
        UpdateSortArrow();
    }

    /// <summary>A header click sorts by Name, Size or Modified; Folder, New Name and Status are not sortable.</summary>
    private void OnColumnClick(object? sender, ColumnClickEventArgs e)
    {
        if (e.Column >= 0 && e.Column < _columns.Order.Count && _columns.SortColumnAt(e.Column) is FileListColumn column)
        {
            _model.SortBy(column);
        }
    }

    /// <summary>After a sort or a filter (NS-70: the row count may change): sizes the list to the model's rows before
    /// any row is selected, selects the model's selected rows again (the same items, at their new rows), scrolls to the
    /// first of them, or to the top when the list shrank and nothing is selected, shows or hides the empty message,
    /// and redraws the count, the conflict line and the sort arrow. The rows repaint from the new order.</summary>
    private void RenderOrder()
    {
        if (IsDisposed)
        {
            return;
        }

        int count = _model.Items.Count;
        int oldCount = _shownCount < 0 ? 0 : _shownCount;

        // Render must not clear the selection again for this list.
        _shownItems = _model.Items;

        // Cleared before the size changes, so no selected or focused row points past the new end.
        if (_list.IsHandleCreated)
        {
            SetAllRowsSelected(false);
        }

        _list.VirtualListSize = count;
        _shownCount = count;
        if (_list.IsHandleCreated && count > 0)
        {
            int[] rows = FileListRows.InRange(_model.SelectedRows, count);
            if (rows.Length > 0 && rows.Length == count)
            {
                SetAllRowsSelected(true);
            }
            else
            {
                foreach (int row in rows)
                {
                    SetRowSelected(row);
                }
            }

            if (FileListRows.ScrollTarget(rows, oldCount, count) is int target)
            {
                if (rows.Length > 0)
                {
                    _list.FocusedItem = _list.Items[target];
                }

                _list.EnsureVisible(target);
            }
        }

        string? text = _model.Message;
        _message.Text = text ?? "";
        _message.Visible = text != null;
        _list.Invalidate();
        RenderCount();
        RenderSummary();
        UpdateSortArrow();
    }

    /// <summary>Draws the system's sort arrow on the sorted column's header, and on no other; none until the user sorts.</summary>
    private void UpdateSortArrow()
    {
        if (IsDisposed || !_list.IsHandleCreated)
        {
            return;
        }

        IntPtr header = SendMessage(_list.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
        if (header == IntPtr.Zero)
        {
            return;
        }

        int sorted = _model.IsSortChosen ? _columns.SortArrowIndex(_model.Sort.Column) : -1;

        for (int column = 0; column < _list.Columns.Count; column++)
        {
            var item = new HdItem { mask = HdiFormat };
            if (SendMessage(header, HdmGetItem, (IntPtr)column, ref item) == IntPtr.Zero)
            {
                continue;
            }

            item.fmt &= ~(HdfSortUp | HdfSortDown);
            if (column == sorted)
            {
                item.fmt |= _model.Sort.Descending ? HdfSortDown : HdfSortUp;
            }

            SendMessage(header, HdmSetItem, (IntPtr)column, ref item);
        }
    }

    /// <summary>The "N of M files" count under the list: it reads the model's batch at once, never waiting for the preview.</summary>
    private void RenderCount()
    {
        if (IsDisposed)
        {
            return;
        }

        string text = _model.State == FileListState.Loaded && _model.Items.Count > 0
            ? FileListText.SelectionCount(_model.Batch.Count, _model.Items.Count, _model.IsNarrowedByFilter, CultureInfo.CurrentCulture)
            : "";
        _count.Text = text;
        _count.AccessibleName = text;
        UpdateStrip();
    }

    /// <summary>The "N conflicts" line under the list; empty when there is none.</summary>
    private void RenderSummary()
    {
        string text = _preview.ConflictSummary;
        _summary.Text = text;
        _summary.AccessibleName = text;
        _summary.ForeColor = Palette.ConflictText;
        UpdateStrip();
    }

    private void UpdateStrip()
    {
        _strip.Visible = _count.Text.Length > 0 || _summary.Text.Length > 0;
    }

    /// <summary>Every selection event only schedules one read; the read takes the list's selection as it is then.</summary>
    private void ScheduleSelectionRead()
    {
        if (_selectionReadPending || !IsHandleCreated)
        {
            return;
        }

        _selectionReadPending = true;
        BeginInvoke(new Action(ReadSelection));
    }

    private void ReadSelection()
    {
        _selectionReadPending = false;
        if (IsDisposed)
        {
            return;
        }

        // One O(k) copy: in virtual mode each SelectedIndices[i] walks from the start. A read queued before a filter
        // shrank the list keeps only the rows still in it.
        var rows = new int[_list.SelectedIndices.Count];
        _list.SelectedIndices.CopyTo(rows, 0);
        _model.SetSelection(FileListRows.InRange(rows, _model.Items.Count));
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.A && e.Control && !e.Alt && !e.Shift)
        {
            SetAllRowsSelected(true);
            ScheduleSelectionRead();
            e.Handled = e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt && !e.Shift)
        {
            RequestOpen(_list.FocusedItem?.Index ?? -1);
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    private void OnListDoubleClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        ListViewItem? item = _list.HitTest(e.Location).Item;
        if (item != null)
        {
            RequestOpen(item.Index);
        }
    }

    /// <summary>Asks to open the row when it is a folder. The index can be stale (a filter shrank the list): it is bounds-checked.</summary>
    private void RequestOpen(int index)
    {
        if (IsDisposed || index < 0 || index >= _model.Items.Count)
        {
            return;
        }

        string? target = FolderRowTarget.Of(_model.Items[index]);
        if (target != null)
        {
            FolderOpenRequested?.Invoke(this, target);
        }
    }

    /// <summary>Selects or clears every row with one message: 10 000 SelectedIndices.Add calls would freeze the window.</summary>
    private void SetAllRowsSelected(bool selected)
    {
        var item = new LvItem { stateMask = LvisSelected, state = selected ? LvisSelected : 0 };
        SendMessage(_list.Handle, LvmSetItemState, (IntPtr)(-1), ref item);
    }

    /// <summary>Selects one row with one message (never SelectedIndices.Add in a loop: it is slow on thousands of rows).</summary>
    private void SetRowSelected(int row)
    {
        var item = new LvItem { stateMask = LvisSelected, state = LvisSelected };
        SendMessage(_list.Handle, LvmSetItemState, (IntPtr)row, ref item);
    }

    /// <summary>Repaints the visible rows: the virtual list asks for them again. The size and scroll position stay.</summary>
    private void RenderPreview()
    {
        if (IsDisposed)
        {
            return;
        }

        _list.Invalidate();
        RenderSummary();
        if (_preview.HasFailed)
        {
            _showStatus(Strings.SomethingWentWrong);
        }
    }

    /// <summary>Recreates the bold font from the list's current font, then disposes the old one once unused.</summary>
    private void RefreshBoldFont()
    {
        Font old = _boldFont;
        _boldFont = new Font(_list.Font, FontStyle.Bold);
        _list.Invalidate();
        old.Dispose();
    }

    private ListViewItem BuildRow(int index)
    {
        // WinForms can ask for a row while the size changes: answer with an empty row.
        FileListColumns columns = _columns;
        var cells = new string[columns.Order.Count];
        if (index < 0 || index >= _model.Items.Count)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = "";
            }

            return new ListViewItem(cells);
        }

        RenameItem item = _model.Items[index];
        CultureInfo culture = CultureInfo.CurrentCulture;
        PreviewCell cell = _preview.CellForRow(index);
        StatusCell status = _preview.StatusForRow(index);
        for (int i = 0; i < cells.Length; i++)
        {
            cells[i] = columns.Order[i] switch
            {
                ShownColumn.Name => item.Name,
                ShownColumn.Folder => _model.FolderPath is string root ? FileListText.RelativeFolder(root, item) : "",
                ShownColumn.NewName => cell.Text,
                ShownColumn.Status => status.Text,
                ShownColumn.Size => FileListText.Size(item, culture),
                _ => FileListText.Modified(item, culture),
            };
        }

        var row = new ListViewItem(cells, item.Kind == ItemKind.Folder ? FolderImage : FileImage)
        {
            UseItemStyleForSubItems = false,
        };
        int newName = columns.IndexOf(ShownColumn.NewName);
        StyleNewName(row.SubItems[newName], cell.Kind);
        if (status.IsConflict)
        {
            Color color = Palette.ConflictText;
            row.SubItems[newName].ForeColor = color;
            row.SubItems[columns.IndexOf(ShownColumn.Status)].ForeColor = color;
        }

        return row;
    }

    private void StyleNewName(ListViewItem.ListViewSubItem subItem, PreviewCellKind kind)
    {
        // A Windows contrast theme wins: Changed and Error then use the theme's text colour (bold and the prefix still mark them).
        switch (kind)
        {
            case PreviewCellKind.Changed:
                subItem.ForeColor = Palette.ChangedText;
                subItem.Font = _boldFont;
                break;
            case PreviewCellKind.Unchanged:
                subItem.ForeColor = SystemColors.GrayText;
                break;
            case PreviewCellKind.Error:
                subItem.ForeColor = Palette.ConflictText;
                break;
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

    private const int LvmSetItemState = 0x102B;
    private const int LvisSelected = 0x2;
    private const int LvmGetHeader = 0x101F;
    private const int HdmGetItem = 0x120B;
    private const int HdmSetItem = 0x120C;
    private const int HdiFormat = 0x4;
    private const int HdfSortUp = 0x400;
    private const int HdfSortDown = 0x200;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref LvItem lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref HdItem lParam);

    /// <summary>The Win32 HDITEMW (the header control's column record).</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct HdItem
    {
        public uint mask;
        public int cxy;
        public IntPtr pszText;
        public IntPtr hbm;
        public int cchTextMax;
        public int fmt;
        public IntPtr lParam;
        public int iImage;
        public int iOrder;
        public uint type;
        public IntPtr pvFilter;
        public uint state;
    }

    /// <summary>The Win32 LVITEM (comctl32 version 6 layout).</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LvItem
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public int state;
        public int stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
        public int iIndent;
        public int iGroupId;
        public uint cColumns;
        public IntPtr puColumns;
        public IntPtr piColFmt;
        public int iGroup;
    }

    /// <summary>The shell's two stock icons for the list: folder and generic file.</summary>
    private static class StockIcons
    {
        public const int Folder = 3;
        public const int File = 0;

        private const uint GetIcon = 0x100;
        private const uint SmallIcon = 0x1;

        public static Bitmap? Load(int stockIconId, Size size)
        {
            var info = new StockIconInfo { cbSize = (uint)Marshal.SizeOf(typeof(StockIconInfo)) };
            if (SHGetStockIconInfo(stockIconId, GetIcon | SmallIcon, ref info) != 0 || info.hIcon == IntPtr.Zero)
            {
                return null;
            }

            Bitmap? bitmap = null;
            try
            {
                using Icon icon = Icon.FromHandle(info.hIcon);
                bitmap = new Bitmap(size.Width, size.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.DrawIcon(icon, new Rectangle(Point.Empty, size));
                }

                return bitmap;
            }
            catch (Exception exception) when (exception is ExternalException or ArgumentException)
            {
                // A GDI+ failure must never stop startup: the caller shows a blank image instead.
                bitmap?.Dispose();
                return null;
            }
            finally
            {
                DestroyIcon(info.hIcon);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StockIconInfo
        {
            public uint cbSize;
            public IntPtr hIcon;
            public int iSysImageIndex;
            public int iIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szPath;
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetStockIconInfo(int siid, uint uFlags, ref StockIconInfo psii);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}
