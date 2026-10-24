using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Core;
using NameShift.Ui.Presets;

namespace NameShift.Ui.Rules;

/// <summary>
/// The rule panels in pipeline order and the Core rules of the enabled ones. Runs on the UI thread. A change in any
/// panel rebuilds <see cref="Rules"/> and raises <see cref="RulesChanged"/> at once: no timer, the preview debounces.
/// </summary>
public sealed class RulePanelHostModel
{
    private bool _batching;
    private bool _changedWhileBatching;

    public RulePanelHostModel(IEnumerable<IRulePanelModel> panels)
    {
        if (panels == null)
        {
            throw new ArgumentNullException(nameof(panels));
        }

        var list = panels.ToList();
        foreach (IRulePanelModel panel in list)
        {
            if (panel == null)
            {
                throw new ArgumentException("A panel is null.", nameof(panels));
            }

            if (!Enum.IsDefined(typeof(RuleStage), panel.Stage))
            {
                throw new ArgumentException($"{panel.Title} has an undefined stage.", nameof(panels));
            }
        }

        if (list.Select(p => p.Stage).Distinct().Count() != list.Count)
        {
            throw new ArgumentException("Two panels have the same stage.", nameof(panels));
        }

        Panels = list.OrderBy(p => (int)p.Stage).ToList().AsReadOnly();
        foreach (IRulePanelModel panel in Panels)
        {
            panel.Changed += (_, _) => OnPanelChanged();
        }

        Rules = BuildRules();
    }

    /// <summary>The panels in pipeline order.</summary>
    public IReadOnlyList<IRulePanelModel> Panels { get; }

    /// <summary>For every enabled panel in order, its rule when it has one. This is what the preview (NS-13) runs.</summary>
    public IReadOnlyList<IRenameRule> Rules { get; private set; }

    /// <summary>Raised synchronously after <see cref="Rules"/> was rebuilt.</summary>
    public event EventHandler? RulesChanged;

    /// <summary>True when the panel is enabled and has a non-default setting: the frame highlights it.</summary>
    public static bool IsActive(IRulePanelModel panel) => panel.Enabled && !panel.IsDefault;

    /// <summary>True when there is something to reset: the panel is enabled or has a non-default setting.</summary>
    public static bool CanReset(IRulePanelModel panel) => panel.Enabled || !panel.IsDefault;

    /// <summary>True when at least one panel has something to reset.</summary>
    public bool CanResetAny => Panels.Any(CanReset);

    /// <summary>Resets every panel that has something to reset (settings to defaults, unchecked). Rebuilds Rules and
    /// raises RulesChanged once at most, after the last panel, not once per panel. Each reset panel still raises its own
    /// Changed (its control resyncs); a panel already default and unchecked is left alone, so it raises nothing.</summary>
    public void ResetAll()
    {
        _batching = true;
        _changedWhileBatching = false;
        try
        {
            foreach (IRulePanelModel panel in Panels.Where(CanReset).ToList())
            {
                panel.Reset();
            }
        }
        finally
        {
            _batching = false;
        }

        if (_changedWhileBatching)
        {
            OnPanelChanged();
        }
    }

    /// <summary>Applies a preset (NS-36) so the panels equal it exactly: each panel the preset has an entry for gets its
    /// settings and its Enabled; every other panel that has something to reset is reset and unchecked. Entries for a
    /// stage this host has no panel for are ignored. Rebuilds Rules and raises RulesChanged exactly once, after the last
    /// panel, even when nothing changed. Each panel still raises its own Changed (its control resyncs).</summary>
    /// <exception cref="ArgumentNullException">The preset is null.</exception>
    public void ApplyPreset(Preset preset)
    {
        if (preset == null)
        {
            throw new ArgumentNullException(nameof(preset));
        }

        _batching = true;
        try
        {
            foreach (IRulePanelModel panel in Panels)
            {
                PresetPanel? entry = preset.Panels.FirstOrDefault(p => p.Stage == panel.Stage);
                if (entry != null)
                {
                    panel.LoadSettings(entry.Settings);
                    panel.Enabled = entry.Enabled;
                }
                else if (CanReset(panel))
                {
                    panel.Reset();
                }
            }
        }
        finally
        {
            _batching = false;
        }

        OnPanelChanged();
    }

    private void OnPanelChanged()
    {
        if (_batching)
        {
            _changedWhileBatching = true;
            return;
        }

        Rules = BuildRules();
        RulesChanged?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<IRenameRule> BuildRules()
    {
        var rules = new List<IRenameRule>();
        foreach (IRulePanelModel panel in Panels)
        {
            if (!panel.Enabled)
            {
                continue;
            }

            IRenameRule? rule = panel.BuildRule();
            if (rule == null)
            {
                continue;
            }

            if (rule.Stage != panel.Stage)
            {
                throw new InvalidOperationException(
                    $"The {panel.Title} panel built a rule for stage {rule.Stage}, not stage {panel.Stage}.");
            }

            rules.Add(rule);
        }

        return rules.AsReadOnly();
    }
}
