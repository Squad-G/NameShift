using System;
using System.Windows.Forms;
using NameShift.Disk;
using NameShift.Ui.Actions;
using NameShift.Ui.Files;
using NameShift.Ui.Folders;
using NameShift.Ui.Presets;
using NameShift.Ui.Settings;

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
        // The window's only place that names the real history folder (the command line's is NameShift.Cmd.Exe's Main).
        var renamer = new JournaledBatchRenamer(new JournaledRenamer(JournaledRenamer.DefaultHistoryRoot));

        // The only place that names the real settings folder (NS-37). Read before the message loop: two small files,
        // never throwing; the window saves them again at close.
        var settingsStore = new AppSettingsStore(AppSettingsStore.DefaultFolder);
        AppSettings saved = settingsStore.LoadSettings();
        Preset? rules = settingsStore.LoadRules();
        Application.Run(new MainForm(
            new DiskFolderSource(),
            new DiskFileListSource(),
            renamer,
            args,
            currentDirectory,
            new PresetStore(PresetStore.DefaultFolder),
            settingsStore,
            saved,
            rules));
    }
}
