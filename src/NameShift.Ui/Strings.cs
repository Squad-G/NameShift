using System;
using System.Globalization;
using System.Resources;

namespace NameShift.Ui;

/// <summary>Every visible string of the app, from Strings.resx. One property per key.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new ResourceManager("NameShift.Ui.Strings", typeof(Strings).Assembly);

    public static string AppTitle => Get(nameof(AppTitle));

    public static string Desktop => Get(nameof(Desktop));

    public static string Documents => Get(nameof(Documents));

    public static string Downloads => Get(nameof(Downloads));

    public static string Pictures => Get(nameof(Pictures));

    public static string Loading => Get(nameof(Loading));

    public static string DriveNotReady => Get(nameof(DriveNotReady));

    public static string AccessDenied => Get(nameof(AccessDenied));

    public static string FolderNotFound => Get(nameof(FolderNotFound));

    public static string FolderUnavailable => Get(nameof(FolderUnavailable));

    public static string PathBarLabel => Get(nameof(PathBarLabel));

    public static string PathInvalid => Get(nameof(PathInvalid));

    /// <summary>"Folder not found: {0}".</summary>
    public static string PathNotFound => Get(nameof(PathNotFound));

    /// <summary>"That is a file, not a folder: {0}".</summary>
    public static string PathNotAFolder => Get(nameof(PathNotAFolder));

    public static string PathBarHint => Get(nameof(PathBarHint));

    public static string FolderTreeName => Get(nameof(FolderTreeName));

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException("Missing string resource: " + key);
}
