using System;

namespace NameShift.Ui.Files;

/// <summary>What reading one file's photo date gave.</summary>
internal enum PhotoReadOutcome
{
    /// <summary>The bytes hold a photo date (cached).</summary>
    Date,

    /// <summary>The file was read and its bytes hold no photo date: not a JPEG, no Exif date, damaged (cached).</summary>
    NoDate,

    /// <summary>The file could not be read: an I/O or access error, or a cloud placeholder that was not opened (not
    /// cached, so the next run tries again).</summary>
    Failed,
}

/// <summary>The answer for one file: one of the three outcomes, with the date for <see cref="PhotoReadOutcome.Date"/>.</summary>
internal readonly struct PhotoRead
{
    /// <summary>The bytes hold no date: cached.</summary>
    public static readonly PhotoRead NoDate = new PhotoRead(PhotoReadOutcome.NoDate, null);

    /// <summary>Could not read: not cached.</summary>
    public static readonly PhotoRead Failed = new PhotoRead(PhotoReadOutcome.Failed, null);

    private PhotoRead(PhotoReadOutcome outcome, DateTime? date)
    {
        Outcome = outcome;
        Date = date;
    }

    /// <summary>Which of the three answers this is.</summary>
    public PhotoReadOutcome Outcome { get; }

    /// <summary>The photo date as read (no conversion); set only for <see cref="PhotoReadOutcome.Date"/>.</summary>
    public DateTime? Date { get; }

    /// <summary>The bytes hold this date.</summary>
    public static PhotoRead Found(DateTime date) => new PhotoRead(PhotoReadOutcome.Date, date);
}

/// <summary>Reads one file's photo date. May block. Never throws for an unreadable file
/// (<see cref="PhotoRead.Failed"/>). Must be safe to call from several threads at once.</summary>
internal interface IPhotoFile
{
    /// <summary>The photo date of the file at <paramref name="path"/>, or why there is none.</summary>
    PhotoRead Read(string path);
}
