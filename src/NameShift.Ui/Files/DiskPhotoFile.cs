using System;
using System.IO;
using System.Security;
using NameShift.Core;

namespace NameShift.Ui.Files;

/// <summary>Reads a photo date from the real disk.
/// <list type="bullet">
/// <item>The attributes are read first (a stat, which never makes a cloud service download the file). An online-only
/// cloud placeholder (Offline, RecallOnOpen or RecallOnDataAccess) is never opened: <see cref="PhotoRead.Failed"/>.
/// ReadOnly, Hidden, System and ReparsePoint alone do not skip a file.</item>
/// <item>The file is opened for reading with sharing for read, write and delete, so a photo open elsewhere still
/// reads. <see cref="PhotoDate.Read"/> reads it forward only and stops after 256 KiB, whatever its size; nothing else
/// is read.</item>
/// <item>An <see cref="IOException"/> (a missing file, a sharing violation), <see cref="UnauthorizedAccessException"/>,
/// <see cref="SecurityException"/> or <see cref="NotSupportedException"/> is <see cref="PhotoRead.Failed"/>; any other
/// exception is a bug and is not caught.</item>
/// </list>
/// May block; safe from several threads at once.</summary>
internal sealed class DiskPhotoFile : IPhotoFile
{
    /// <summary>FILE_ATTRIBUTE_OFFLINE.</summary>
    internal const int OfflineFlag = 0x1000;

    /// <summary>FILE_ATTRIBUTE_RECALL_ON_OPEN (not a <see cref="FileAttributes"/> member on .NET Framework).</summary>
    internal const int RecallOnOpenFlag = 0x40000;

    /// <summary>FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS (not a <see cref="FileAttributes"/> member on .NET Framework).</summary>
    internal const int RecallOnDataAccessFlag = 0x400000;

    /// <summary>Any of these flags marks a file whose content is not on the local disk (0x441000).</summary>
    internal const int PlaceholderMask = OfflineFlag | RecallOnOpenFlag | RecallOnDataAccessFlag;

    private readonly Func<string, FileAttributes> _getAttributes;
    private readonly Func<string, Stream> _open;

    /// <summary>The real disk.</summary>
    public DiskPhotoFile()
        : this(File.GetAttributes, OpenShared)
    {
    }

    /// <summary>For tests: the attribute read and the open, so a test can prove a placeholder is never opened.</summary>
    internal DiskPhotoFile(Func<string, FileAttributes> getAttributes, Func<string, Stream> open)
    {
        _getAttributes = getAttributes ?? throw new ArgumentNullException(nameof(getAttributes));
        _open = open ?? throw new ArgumentNullException(nameof(open));
    }

    /// <summary>True when the attributes mark an online-only cloud placeholder (opening it would download it).</summary>
    internal static bool IsPlaceholder(FileAttributes attributes) => ((int)attributes & PlaceholderMask) != 0;

    /// <inheritdoc/>
    public PhotoRead Read(string path)
    {
        try
        {
            if (IsPlaceholder(_getAttributes(path)))
            {
                return PhotoRead.Failed;
            }

            using Stream stream = _open(path);
            DateTime? date = PhotoDate.Read(stream);
            return date is null ? PhotoRead.NoDate : PhotoRead.Found(date.Value);
        }
        catch (Exception e) when (IsUnreadable(e))
        {
            return PhotoRead.Failed;
        }
    }

    private static bool IsUnreadable(Exception e) =>
        e is IOException || e is UnauthorizedAccessException || e is SecurityException || e is NotSupportedException;

    private static Stream OpenShared(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
}
