namespace NameShift.Ui;

/// <summary>The text of a path argument after NS-47's steps (a) to (h): a full path, a message, or blank.</summary>
internal sealed class NormalizedPath
{
    private NormalizedPath(string? fullPath, string? message, bool isBlank)
    {
        FullPath = fullPath;
        Message = message;
        IsBlank = isBlank;
    }

    internal static NormalizedPath Blank { get; } = new NormalizedPath(null, null, true);

    internal static NormalizedPath Invalid { get; } = new NormalizedPath(null, Strings.PathInvalid, false);

    /// <summary>The normalised full path, or null.</summary>
    internal string? FullPath { get; }

    /// <summary>The message when the text is not a usable path, or null.</summary>
    internal string? Message { get; }

    /// <summary>No text at all (null, white space or only quotes).</summary>
    internal bool IsBlank { get; }

    internal static NormalizedPath Ok(string fullPath) => new NormalizedPath(fullPath, null, false);
}
