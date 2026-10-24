using System.Globalization;

namespace NameShift.Ui.Presets;

/// <summary>The words of the Presets menu and of its results, formatted for the status bar and the questions. A preset
/// name shows as typed (trimmed) in a status line and with its ampersands doubled in a menu.</summary>
public static class PresetText
{
    /// <summary>The menu text of a preset: "&amp;" doubled, so a name never gets an Alt key.</summary>
    public static string MenuText(string name) => name.Replace("&", "&&");

    /// <summary>Preset "X" saved.</summary>
    public static string Saved(string name) => Format(Strings.PresetSaved, name);

    /// <summary>Preset "X" loaded.</summary>
    public static string Loaded(string name) => Format(Strings.PresetLoaded, name);

    /// <summary>Preset "X" deleted.</summary>
    public static string Deleted(string name) => Format(Strings.PresetDeleted, name);

    /// <summary>The preset "X" was not found.</summary>
    public static string NotFound(string name) => Format(Strings.PresetNotFound, name);

    /// <summary>The preset could not be saved: message.</summary>
    public static string SaveFailed(string? message) => Format(Strings.PresetSaveFailed, (message ?? "").Trim());

    /// <summary>The preset could not be deleted: message.</summary>
    public static string DeleteFailed(string? message) => Format(Strings.PresetDeleteFailed, (message ?? "").Trim());

    /// <summary>Replace the preset "X"?</summary>
    public static string ReplaceQuestion(string name) => Format(Strings.PresetReplaceQuestion, name);

    /// <summary>Delete the preset "X"?</summary>
    public static string DeleteQuestion(string name) => Format(Strings.PresetDeleteQuestion, name);

    /// <summary>"1 preset could not be read." or "N presets could not be read."; null for none.</summary>
    public static string? Unreadable(int count)
    {
        if (count <= 0)
        {
            return null;
        }

        return count == 1
            ? Strings.PresetUnreadableOne
            : string.Format(
                CultureInfo.CurrentCulture,
                Strings.PresetUnreadableMany,
                count.ToString("N0", CultureInfo.CurrentCulture));
    }

    private static string Format(string format, string argument) =>
        string.Format(CultureInfo.CurrentCulture, format, argument);
}
