namespace NameShift.Ui;

/// <summary>What to open at start or on a drop: a folder (and maybe a file to select), a message, or nothing.</summary>
public sealed class OpenRequest
{
    private OpenRequest(string? folder, string? fileName, string? message)
    {
        Folder = folder;
        FileName = fileName;
        Message = message;
    }

    /// <summary>Nothing to open and nothing to say: the window opens as usual.</summary>
    public static OpenRequest Nothing { get; } = new OpenRequest(null, null, null);

    /// <summary>The folder to open (checked on disk), or null.</summary>
    public string? Folder { get; }

    /// <summary>The file to select in <see cref="Folder"/>, or null.</summary>
    public string? FileName { get; }

    /// <summary>The status-bar message when nothing opens, or null.</summary>
    public string? Message { get; }

    internal static OpenRequest ForFolder(string folder) => new OpenRequest(folder, null, null);

    internal static OpenRequest ForFile(string folder, string fileName) => new OpenRequest(folder, fileName, null);

    internal static OpenRequest ForMessage(string message) => new OpenRequest(null, null, message);
}
