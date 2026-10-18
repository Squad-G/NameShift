using System.IO;

namespace NameShift.Disk;

/// <summary>Normalized folder paths: full, no trailing separator except on a root ("C:\"); compared ordinal
/// ignore-case.</summary>
internal static class FolderKey
{
    /// <summary>The normalized key of a rooted folder path.</summary>
    public static string Of(string folderPath)
    {
        var full = Path.GetFullPath(folderPath);
        var root = Path.GetPathRoot(full) ?? string.Empty;
        return full.Length > root.Length ? full.TrimEnd('\\', '/') : full;
    }

    /// <summary>The key of the containing folder, or null for a root.</summary>
    public static string? Parent(string key) => Path.GetDirectoryName(key);

    /// <summary>The number of separators in the key: deeper folders have more.</summary>
    public static int Depth(string key)
    {
        var depth = 0;
        foreach (var c in key)
        {
            if (c == '\\' || c == '/')
            {
                depth++;
            }
        }

        return depth;
    }
}
