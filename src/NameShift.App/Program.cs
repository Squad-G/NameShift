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
    private static void Main(string[] args)
    {
        // Read first: a relative path argument is resolved against the directory the program started in.
        string currentDirectory = Environment.CurrentDirectory;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // The only place the real history folder is named.
        var renamer = new JournaledBatchRenamer(new JournaledRenamer(JournaledRenamer.DefaultHistoryRoot));
        Application.Run(new MainForm(new DiskFolderSource(), new DiskFileListSource(), renamer, args, currentDirectory));
    }
}
