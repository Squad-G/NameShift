namespace NameShift.Ui.Presets;

/// <summary>The trimmed text of a typed preset name and its verdict.</summary>
public sealed class PresetNameCheck
{
    internal PresetNameCheck(string name, PresetNameVerdict verdict)
    {
        Name = name;
        Verdict = verdict;
    }

    /// <summary>The text without the white space around it ("" for null).</summary>
    public string Name { get; }

    public PresetNameVerdict Verdict { get; }
}
