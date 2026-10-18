using System.Windows.Forms;

namespace NameShift.App.Controls;

/// <summary>A list view that paints double-buffered, so repainting the visible rows on every update does not flicker.</summary>
internal sealed class BufferedListView : ListView
{
    public BufferedListView()
    {
        DoubleBuffered = true;
    }
}