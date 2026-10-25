using System;
using System.Globalization;

namespace NameShift.Ui;

/// <summary>The status-bar text after opening a folder from the file list.</summary>
public static class FolderOpenText
{
    /// <summary>The text for a failed open, or null when there is nothing to say (the status bar is cleared by the
    /// folder change itself).</summary>
    public static string? For(PathBarResult result, string path, CultureInfo culture)
    {
        switch (result)
        {
            case PathBarResult.NotFound:
                return string.Format(culture, Strings.PathNotFound, path);
            case PathBarResult.NotAFolder:
                return string.Format(culture, Strings.PathNotAFolder, path);
            case PathBarResult.Invalid:
            case PathBarResult.Empty:
                return Strings.PathInvalid;
            case PathBarResult.Navigated:
            case PathBarResult.Unchanged:
            case PathBarResult.Superseded:
                return null;
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result, null);
        }
    }
}
