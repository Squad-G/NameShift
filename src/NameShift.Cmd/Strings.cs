using System;
using System.Globalization;
using System.Resources;

namespace NameShift.Cmd;

/// <summary>Every text the command line writes itself, from Strings.resx. One property per key. The window's texts
/// (results, conflicts, rule errors, hints) come from NameShift.Ui.</summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new ResourceManager("NameShift.Cmd.Strings", typeof(Strings).Assembly);

    /// <summary>The usage line that ends every usage error.</summary>
    public static string Usage => Get(nameof(Usage));

    public static string NoCommand => Get(nameof(NoCommand));

    /// <summary>Unknown command "{0}".</summary>
    public static string UnknownCommand => Get(nameof(UnknownCommand));

    /// <summary>Unknown option "{0}".</summary>
    public static string UnknownOption => Get(nameof(UnknownOption));

    public static string ReplaceNeedsValues => Get(nameof(ReplaceNeedsValues));

    public static string CaseNeedsValue => Get(nameof(CaseNeedsValue));

    /// <summary>"{0}" is not a value of --case: ... (lists the four values).</summary>
    public static string BadCase => Get(nameof(BadCase));

    /// <summary>"{0} can be given only once." ({0} is --replace or --case).</summary>
    public static string RepeatedOption => Get(nameof(RepeatedOption));

    public static string EmptyFind => Get(nameof(EmptyFind));

    /// <summary>Give one folder only: "{0}" and "{1}" were both given.</summary>
    public static string TwoFolders => Get(nameof(TwoFolders));

    public static string NoFolder => Get(nameof(NoFolder));

    public static string NoRule => Get(nameof(NoRule));

    /// <summary>"The folder {0} contains a quote: ...": what cmd makes of a quoted folder that ends with a backslash.</summary>
    public static string QuoteInFolder => Get(nameof(QuoteInFolder));

    /// <summary>"{0}" is not a valid folder path.</summary>
    public static string InvalidFolder => Get(nameof(InvalidFolder));

    /// <summary>"{0} is a file, not a folder.".</summary>
    public static string FolderIsFile => Get(nameof(FolderIsFile));

    /// <summary>"Folder not found: {0}".</summary>
    public static string FolderNotFound => Get(nameof(FolderNotFound));

    /// <summary>"Cannot list {0}: {1}": the folder, then the window's folder problem text.</summary>
    public static string CannotList => Get(nameof(CannotList));

    public static string NothingToRename => Get(nameof(NothingToRename));

    /// <summary>"{0} -&gt; {1}": the old and the new file name of a dry run.</summary>
    public static string DryRunLine => Get(nameof(DryRunLine));

    /// <summary>"{0}: {1}": a file name, then its conflicts or its rule error.</summary>
    public static string ItemLine => Get(nameof(ItemLine));

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException("Missing string resource: " + key);
}
