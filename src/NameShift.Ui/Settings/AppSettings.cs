using System;

namespace NameShift.Ui.Settings;

/// <summary>The window facts kept in <c>settings.json</c> between two runs (NS-37): where the window was, the folder
/// tree's splitter and the last folder. Nothing of the working state but the rules (kept in <c>last-rules.json</c>):
/// no filter, sort, selection, sub-folders box or lock. Immutable; a new member is one more constructor argument.</summary>
public sealed class AppSettings
{
    /// <param name="window">The placement, or null when there is none to use.</param>
    /// <param name="mainSplit">The folder tree's splitter in 96-DPI logical pixels, or null for the default.</param>
    /// <param name="lastFolder">The folder shown at close, or null.</param>
    /// <exception cref="ArgumentOutOfRangeException">The splitter value is negative.</exception>
    public AppSettings(SavedWindow? window, int? mainSplit, string? lastFolder)
    {
        if (mainSplit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mainSplit), "The splitter value cannot be negative.");
        }

        Window = window;
        MainSplit = mainSplit;
        LastFolder = string.IsNullOrWhiteSpace(lastFolder) ? null : lastFolder;
    }

    /// <summary>No value: the window opens as it does without a file.</summary>
    public static AppSettings Empty { get; } = new AppSettings(null, null, null);

    /// <summary>The placement, or null when there is no usable one.</summary>
    public SavedWindow? Window { get; }

    /// <summary>The folder tree's SplitterDistance in 96-DPI logical pixels; null means the default (260).</summary>
    public int? MainSplit { get; }

    /// <summary>The last folder shown, exactly as it was; null or a path that is not blank.</summary>
    public string? LastFolder { get; }
}
