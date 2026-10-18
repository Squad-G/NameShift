using System.Drawing;
using System.Windows.Forms;

namespace NameShift.App;

/// <summary>Main window: folder tree (left), file list with live new-name preview (right), rule panels (bottom).</summary>
internal sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "NameShift";
        MinimumSize = new Size(1024, 700);
        StartPosition = FormStartPosition.CenterScreen;
    }
}
