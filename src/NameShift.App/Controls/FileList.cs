using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using NameShift.Core;
using NameShift.Ui;
using NameShift.Ui.Files;
using NameShift.Ui.Preview;

namespace NameShift.App.Controls;

/// <summary>Renders the file list model in a virtual-mode list: only the visible rows are ever built.</summary>
internal sealed class FileList : UserControl
{
    private const int FolderImage = 0;
    private const int FileImage = 1;

    // Green and red with at least 4.5:1 contrast on white; bold and the "Error: " prefix are the non-colour cues.
    private static readonly Color ChangedColor = Color.FromArgb(0x10, 0x7C, 0x10);
    private static readonly Color ErrorColor = Color.FromArgb(0xC4, 0x2B, 0x1C);

    private readonly FileListModel _model;
    private readonly PreviewModel _preview;
    private readonly Action<string> _showStatus;
    private readonly ListView _list = new BufferedListView();
    private readonly Label _message = new Label();
    private readonly ImageList _icons = new ImageList { ColorDepth = ColorDepth.Depth32Bit };
    private readonly List<Bitmap> _iconBitmaps = new List<Bitmap>();
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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _boldFont.Dispose();
            _icons.Dispose();
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
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.HeaderStyle = ColumnHeaderStyle.Nonclickable;
        _list.AccessibleName = Strings.FileListName;
        _list.HandleCreated += (_, _) => SetWindowTheme(_list.Handle, "explorer", null);
        _list.RetrieveVirtualItem += (_, e) => e.Item = BuildRow(e.ItemIndex);
        _list.FontChanged += (_, _) => RefreshBoldFont();

        _list.Columns.Add(Strings.ColumnName, LogicalToDeviceUnits(280));
        _list.Columns.Add(Strings.ColumnNewName, LogicalToDeviceUnits(280));
        _list.Columns.Add(Strings.ColumnSize, LogicalToDeviceUnits(90), HorizontalAlignment.Right);
        _list.Columns.Add(Strings.ColumnModified, LogicalToDeviceUnits(150));

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

        Controls.Add(_list);
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
    }

    /// <summary>Repaints the visible rows: the virtual list asks for them again. The size and scroll position stay.</summary>
    private void RenderPreview()
    {
        if (IsDisposed)
        {
            return;
        }

        _list.Invalidate();
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
        if (index < 0 || index >= _model.Items.Count)
        {
            return new ListViewItem(new[] { "", "", "", "" });
        }

        RenameItem item = _model.Items[index];
        CultureInfo culture = CultureInfo.CurrentCulture;
        PreviewCell cell = _preview.CellForRow(index);
        var row = new ListViewItem(
            new[]
            {
                item.Name,
                cell.Text,
                FileListText.Size(item, culture),
                FileListText.Modified(item, culture),
            },
            item.Kind == ItemKind.Folder ? FolderImage : FileImage)
        {
            UseItemStyleForSubItems = false,
        };
        StyleNewName(row.SubItems[1], cell.Kind);
        return row;
    }

    private void StyleNewName(ListViewItem.ListViewSubItem subItem, PreviewCellKind kind)
    {
        // A Windows contrast theme wins: Changed and Error then use the theme's text colour (bold and the prefix still mark them).
        bool highContrast = SystemInformation.HighContrast;
        switch (kind)
        {
            case PreviewCellKind.Changed:
                subItem.ForeColor = highContrast ? SystemColors.WindowText : ChangedColor;
                subItem.Font = _boldFont;
                break;
            case PreviewCellKind.Unchanged:
                subItem.ForeColor = SystemColors.GrayText;
                break;
            case PreviewCellKind.Error:
                subItem.ForeColor = highContrast ? SystemColors.WindowText : ErrorColor;
                break;
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

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