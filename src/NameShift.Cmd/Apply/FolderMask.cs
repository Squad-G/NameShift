namespace NameShift.Cmd;

/// <summary>Splits a folder argument that ends with a mask: <c>D:\Pics\*.jpg</c> gives the folder <c>D:\Pics</c> and the
/// mask <c>*.jpg</c>. Pure string work: no disk, no <c>PathInput</c>.</summary>
internal static class FolderMask
{
    // The backslash is built from its code, so no editor or shell can turn an escape into another character.
    private static readonly char[] Separators = { (char)92, '/' };
    private static readonly char[] Wildcards = { '*', '?' };

    /// <summary>The folder and the mask, or (<paramref name="text"/>, null) when there is no mask to take. Only the
    /// last segment may hold <c>*</c> or <c>?</c>. No split when the text holds a quote (the runner must see it
    /// whole), the last segment is empty, it holds a colon (as in D:*.jpg), or the only separator is the first character
    /// (a root-relative mask stays the invalid-folder error). A bare drive keeps its separator; with no separator at
    /// all the folder is <c>.</c>, the current directory.</summary>
    public static (string Folder, string? Mask) Split(string text)
    {
        if (text.IndexOf('"') >= 0)
        {
            return (text, null);
        }

        int cut = text.LastIndexOfAny(Separators);
        string last = text.Substring(cut + 1);
        if (cut == 0 || last.Length == 0 || last.IndexOfAny(Wildcards) < 0 || last.IndexOf(':') >= 0)
        {
            return (text, null);
        }

        if (cut < 0)
        {
            return (".", last);
        }

        // A drive root ("D:" then a separator) keeps its separator: the folder is the drive's root, not "D:".
        bool driveRoot = cut == 2 && text[1] == ':';
        return (text.Substring(0, driveRoot ? cut + 1 : cut), last);
    }
}
