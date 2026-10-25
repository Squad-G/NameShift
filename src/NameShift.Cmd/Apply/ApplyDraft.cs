using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;

namespace NameShift.Cmd;

/// <summary>What the readers fill after the loop; the parser turns it into <see cref="ApplyOptions"/>.</summary>
internal sealed class ApplyDraft
{
    public string? Find { get; set; }

    public string? With { get; set; }

    public CaseMode? Case { get; set; }

    public bool DryRun { get; set; }

    public OutputLevel Output { get; set; }

    public string? PresetName { get; set; }

    public string? SaveName { get; set; }

    public bool Overwrite { get; set; }

    /// <summary>The panel models the readers built.</summary>
    public List<IRulePanelModel> Panels { get; } = new List<IRulePanelModel>();

    /// <summary>Find, Case, a panel or a preset: what the missing-rule check tests.</summary>
    public bool HasRule => Find != null || Case.HasValue || Panels.Count > 0 || PresetName != null;

    public ApplyOptions Build(string folder) => new ApplyOptions(folder, Find, With, Case, DryRun, Output,
        new ApplyPanels(Panels), PresetName, SaveName, Overwrite);
}
