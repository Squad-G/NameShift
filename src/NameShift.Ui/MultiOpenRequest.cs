using System.Collections.Generic;

namespace NameShift.Ui;

/// <summary>What to open for several path arguments: one folder, the names to select in it, and how many paths are
/// already known not to be selectable (NS-88).</summary>
public sealed class MultiOpenRequest
{
    internal MultiOpenRequest(string? folder, IReadOnlyList<string> names, int rejected, string? message)
    {
        Folder = folder;
        Names = names;
        Rejected = rejected;
        Message = message;
    }

    /// <summary>The folder to open (the parent of the first path that exists), or null when none exists.</summary>
    public string? Folder { get; }

    /// <summary>The distinct names to select in <see cref="Folder"/>, in argument order. Whether each is listed is
    /// decided by the loaded list, not by the disk.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>The paths known not to be selectable: invalid, not found before the first that exists, in another
    /// folder, or a drive root.</summary>
    public int Rejected { get; }

    /// <summary>When no path exists, the first path's message (as <see cref="OpenTarget.Resolve"/> says it); else null.</summary>
    public string? Message { get; }

    /// <summary>The request for <see cref="OpenSequence"/>: the folder to open, or the message, or nothing.</summary>
    public OpenRequest Request =>
        Folder != null ? OpenRequest.ForFolder(Folder) : Message != null ? OpenRequest.ForMessage(Message) : OpenRequest.Nothing;
}
