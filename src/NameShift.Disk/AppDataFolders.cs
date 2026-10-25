using System;
using System.IO;

namespace NameShift.Disk;

/// <summary>The folders NameShift keeps its files in (the history, the settings, the presets). A profile without a
/// usable special folder (a service account, a broken profile) gives an empty or relative text; the files then go to
/// the temp folder instead of the current directory.</summary>
public static class AppDataFolders
{
    /// <summary>The name of NameShift's own folder inside a special folder.</summary>
    public const string AppFolderName = "NameShift";

    /// <summary>&lt;specialFolderPath&gt;\NameShift when it is a fully qualified path (a drive letter, a colon and a
    /// separator, or a "\\server\share" path); otherwise &lt;tempPath&gt;\NameShift. Never throws for any special folder
    /// text.</summary>
    public static string Resolve(string? specialFolderPath, string tempPath)
    {
        if (tempPath == null)
        {
            throw new ArgumentNullException(nameof(tempPath));
        }

        var root = IsFullyQualified(specialFolderPath) ? specialFolderPath! : tempPath;
        return Path.Combine(root, AppFolderName);
    }

    /// <summary>netstandard2.0 has no Path.IsPathFullyQualified: "\foo" and "C:foo" are rooted but depend on the
    /// current drive or directory, so they do not count.</summary>
    private static bool IsFullyQualified(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path!.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return false;
        }

        if (path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1]))
        {
            return true;
        }

        return path.Length >= 3
            && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'))
            && path[1] == ':'
            && IsSeparator(path[2]);
    }

    private static bool IsSeparator(char c) => c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
}
