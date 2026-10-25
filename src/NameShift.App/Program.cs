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

        // One window at all times (NS-89): a second start hands its arguments to the running window and exits (code 0)
        // before any window or settings file is touched.
        using SingleInstance instance = SingleInstance.Start(args, currentDirectory);
        if (instance.HandedOver)
        {
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        // The window's only place that names the real history folder (the command line's is NameShift.Cmd.Exe's Main).
        var renamer = new JournaledBatchRenamer(new JournaledRenamer(JournaledRenamer.DefaultHistoryRoot));

        // The only place that names the real settings folder (NS-37). Read before the message loop: two small files,
        // never throwing; the window that owns the instance saves them again at close.
        var settingsStore = new AppSettingsStore(AppSettingsStore.DefaultFolder);
        AppSettings saved = settingsStore.LoadSettings();
        Preset? rules = settingsStore.LoadRules();
        var form = new MainForm(
            new DiskFolderSource(),
            new DiskFileListSource(),
            renamer,
            args,
            currentDirectory,
            new PresetStore(PresetStore.DefaultFolder),
            settingsStore,
            saved,
            rules,
            instance.Ownership);

        // Requests from later starts arrive on the window's thread; the pipe opens once the window is shown.
        instance.RequestReceived += (_, request) => form.HandleInstanceRequest(request);
        form.Shown += (_, _) => instance.Serve(form);
        Application.Run(form);
    }
}
