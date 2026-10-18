using System;
using System.Windows.Forms;
using NameShift.Ui.Folders;

namespace NameShift.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm(new DiskFolderSource()));
    }
}
