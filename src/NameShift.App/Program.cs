using System;
using System.Windows.Forms;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;

namespace NameShift.App;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // The only place the real history folder is named.
        var renamer = new JournaledBatchRenamer(new JournaledRenamer(JournaledRenamer.DefaultHistoryRoot));
        Application.Run(new MainForm(new DiskFolderSource(), new DiskFileListSource(), renamer));
    }
}
