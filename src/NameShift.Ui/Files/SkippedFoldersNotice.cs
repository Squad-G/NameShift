using System;
using System.Globalization;

namespace NameShift.Ui.Files;

/// <summary>
/// Tells the window, once per landed load, how many folders the listing could not read (NS-78). It watches
/// <see cref="FileListModel.Changed"/> for the <see cref="FileListState.Loading"/> to <see cref="FileListState.Loaded"/>
/// step: a load that a newer one superseded never lands, so it never reports, and a load without skips says nothing.
/// When a load starts without sub-folders (the box unticked) after a report, <see cref="Withdrawn"/> hands the
/// reported text back, so the window can clear it if it still shows. Created once by the window; every member is
/// called on the UI thread.
/// </summary>
public sealed class SkippedFoldersNotice : IDisposable
{
    private readonly FileListModel _files;
    private readonly CultureInfo _culture;
    // A guard, not load-bearing today: the model raises Loaded only after its own Loading, but a future Changed
    // without a load must never report the same count again.
    private bool _loading;
    private string? _reported;
    private bool _disposed;

    /// <summary>Starts watching the list.</summary>
    public SkippedFoldersNotice(FileListModel files, CultureInfo culture)
    {
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _culture = culture ?? throw new ArgumentNullException(nameof(culture));
        _files.Changed += OnFilesChanged;
    }

    /// <summary>The status text of a load that landed with skipped folders; raised on the calling thread.</summary>
    public event EventHandler<string>? Notice;

    /// <summary>The text last reported, when a load starts without sub-folders; raised on the calling thread.</summary>
    public event EventHandler<string>? Withdrawn;

    /// <summary>The text for this many skipped folders: null for none, the one form for 1, else the many form (N0).</summary>
    public static string? Text(int skippedFolders, CultureInfo culture)
    {
        if (culture == null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        if (skippedFolders <= 0)
        {
            return null;
        }

        return skippedFolders == 1
            ? Strings.SkippedFolder
            : string.Format(culture, Strings.SkippedFolders, skippedFolders.ToString("N0", culture));
    }

    /// <summary>Stops watching. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _files.Changed -= OnFilesChanged;
    }

    private void OnFilesChanged(object? sender, EventArgs e)
    {
        switch (_files.State)
        {
            case FileListState.Loading:
                _loading = true;
                if (!_files.IncludeSubfolders && _reported is string reported)
                {
                    _reported = null;
                    Withdrawn?.Invoke(this, reported);
                }

                break;
            case FileListState.Loaded when _loading:
                _loading = false;
                string? text = Text(_files.SkippedFolders, _culture);
                if (text != null)
                {
                    _reported = text;
                    Notice?.Invoke(this, text);
                }

                break;
            case FileListState.Failed:
                _loading = false;
                break;
        }
    }
}
