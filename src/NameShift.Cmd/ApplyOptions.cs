using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Rules;

namespace NameShift.Cmd;

/// <summary>The options of one <c>apply</c> command line, as parsed. Immutable. Replace and Case keep their own
/// models (and properties); every later rule panel goes through <see cref="Panels"/> and never gets a constructor
/// parameter of its own. <see cref="GivenPanels"/> are the panels the options filled: <see cref="ApplyTo"/> puts each
/// over the host's panel of the same stage, whole.</summary>
internal sealed class ApplyOptions
{
    public ApplyOptions(string folderText, string? find, string? with, CaseMode? caseMode, bool dryRun,
        OutputLevel output = OutputLevel.Normal, ApplyPanels? panels = null, string? presetName = null,
        string? saveName = null, bool overwrite = false)
    {
        FolderText = folderText ?? throw new ArgumentNullException(nameof(folderText));
        if ((find == null) != (with == null))
        {
            throw new ArgumentException("The find and with texts are given together or not at all.", nameof(with));
        }

        if (!Enum.IsDefined(typeof(OutputLevel), output))
        {
            throw new ArgumentOutOfRangeException(nameof(output));
        }

        if (overwrite && saveName == null)
        {
            throw new ArgumentException("--overwrite needs a preset name to save.", nameof(overwrite));
        }

        Find = find;
        With = with;
        Case = caseMode;
        DryRun = dryRun;
        Output = output;
        Panels = panels ?? ApplyPanels.None;
        PresetName = presetName?.Trim();
        SaveName = saveName?.Trim();
        Overwrite = overwrite;

        var given = new List<IRulePanelModel>();
        if (find != null)
        {
            given.Add(new ReplacePanelModel { Find = find, With = with!, Enabled = true });
        }

        if (caseMode.HasValue)
        {
            given.Add(new CasePanelModel { Mode = caseMode.Value, Enabled = true });
        }

        given.AddRange(Panels.Given);
        GivenPanels = given.OrderBy(m => (int)m.Stage).ToList().AsReadOnly();
    }

    /// <summary>The folder, as typed.</summary>
    public string FolderText { get; }

    /// <summary>The text to find; null when --replace is absent.</summary>
    public string? Find { get; }

    /// <summary>The new text; null when --replace is absent.</summary>
    public string? With { get; }

    /// <summary>The case mode; null when --case is absent.</summary>
    public CaseMode? Case { get; }

    /// <summary>True with --dry-run: list the new names, rename nothing.</summary>
    public bool DryRun { get; }

    /// <summary>What a successful run writes: normal, nothing (--quiet) or each file too (--verbose).</summary>
    public OutputLevel Output { get; }

    /// <summary>The Add and Numbering panels the command line filled (null entries when no option of a panel was given).</summary>
    public ApplyPanels Panels { get; }

    /// <summary>The name after --preset, trimmed; null when absent.</summary>
    public string? PresetName { get; }

    /// <summary>The name after --save-preset, trimmed; null when absent.</summary>
    public string? SaveName { get; }

    /// <summary>True with --overwrite (only ever with a save name).</summary>
    public bool Overwrite { get; }

    /// <summary>The models the rule options filled (Replace, Case, Add, Numbering...), each enabled, in stage order.</summary>
    public IReadOnlyList<IRulePanelModel> GivenPanels { get; }

    /// <summary>Puts every given panel over the host's panel of the same stage: Reset, the option's settings, enabled.
    /// What an option does not say is the window's default, never a preset's value.</summary>
    public void ApplyTo(RulePanelHostModel host)
    {
        if (host == null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        foreach (IRulePanelModel model in GivenPanels)
        {
            IRulePanelModel target = host.Panels.FirstOrDefault(p => p.Stage == model.Stage)
                ?? throw new InvalidOperationException("The window has no panel for stage " + model.Stage + ".");
            target.Reset();
            target.LoadSettings(model.SaveSettings());
            target.Enabled = true;
        }
    }

    /// <summary>The rules, built by the window's own panel models over its factory, so the defaults are the window's by
    /// construction (Replace: ignore case, leave the extension; Case: no exceptions). The pipeline sorts them by stage.</summary>
    public IReadOnlyList<IRenameRule> BuildRules()
    {
        var host = new RulePanelHostModel(RulePanelModels.CreateAll());
        ApplyTo(host);
        return host.Rules;
    }
}
