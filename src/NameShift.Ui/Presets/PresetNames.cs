namespace NameShift.Ui.Presets;

/// <summary>The rules for a preset name typed in the window. There is no character rule: the store makes a safe file
/// name from any name.</summary>
public static class PresetNames
{
    /// <summary>The longest name, in characters, after trimming.</summary>
    public const int MaxLength = 100;

    /// <summary>Trims the text and says whether it can be a preset name: null or white space is Empty, more than
    /// <see cref="MaxLength"/> characters after trimming is TooLong.</summary>
    public static PresetNameCheck Check(string? text)
    {
        string name = text?.Trim() ?? "";
        if (name.Length == 0)
        {
            return new PresetNameCheck(name, PresetNameVerdict.Empty);
        }

        return new PresetNameCheck(name, name.Length > MaxLength ? PresetNameVerdict.TooLong : PresetNameVerdict.Ok);
    }
}
