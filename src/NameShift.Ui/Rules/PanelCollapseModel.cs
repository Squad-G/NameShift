using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>
/// Which rule panels are collapsed to their title line (NS-83). Window layout only: it never changes a rule. A panel
/// starts collapsed when its check box is off, unless a saved state says otherwise (NS-84); a panel whose check box goes
/// from off to on opens, whatever ticked it (a click, a key, the title's mnemonic, a preset). Unticking or a Reset leaves
/// the state alone, and opening a panel never ticks it. Runs on the UI thread, like the panel models it listens to.
/// </summary>
public sealed class PanelCollapseModel : IDisposable
{
    private readonly RulePanelHostModel _host;
    private readonly Dictionary<IRulePanelModel, bool> _collapsed = new Dictionary<IRulePanelModel, bool>();
    private readonly Dictionary<IRulePanelModel, bool> _wasEnabled = new Dictionary<IRulePanelModel, bool>();
    private readonly Dictionary<IRulePanelModel, EventHandler> _handlers = new Dictionary<IRulePanelModel, EventHandler>();
    private bool _disposed;

    /// <param name="host">The panels.</param>
    /// <param name="saved">Saved states by stage (NS-84); a panel not in it starts collapsed when unticked. Entries for
    /// a stage the host has no panel for are ignored.</param>
    /// <exception cref="ArgumentNullException">The host is null.</exception>
    public PanelCollapseModel(RulePanelHostModel host, IReadOnlyDictionary<RuleStage, bool>? saved = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        foreach (IRulePanelModel panel in host.Panels)
        {
            _collapsed[panel] = saved != null && saved.TryGetValue(panel.Stage, out bool value) ? value : !panel.Enabled;
            _wasEnabled[panel] = panel.Enabled;
            IRulePanelModel captured = panel;
            EventHandler handler = (_, _) => OnPanelChanged(captured);
            _handlers[panel] = handler;
            panel.Changed += handler;
        }
    }

    /// <summary>Raised once per real change of a panel's state, with that panel.</summary>
    public event EventHandler<IRulePanelModel>? Changed;

    /// <summary>Every panel's state by stage, for saving (NS-84). A new map on each read.</summary>
    public IReadOnlyDictionary<RuleStage, bool> States
    {
        get
        {
            var states = new Dictionary<RuleStage, bool>();
            foreach (IRulePanelModel panel in _host.Panels)
            {
                states[panel.Stage] = _collapsed[panel];
            }

            return states;
        }
    }

    /// <summary>True when the panel shows only its title line.</summary>
    /// <exception cref="ArgumentException">The panel is not one of the host's.</exception>
    public bool IsCollapsed(IRulePanelModel panel)
    {
        Check(panel);
        return _collapsed[panel];
    }

    /// <summary>Collapses an open panel or opens a collapsed one.</summary>
    /// <exception cref="ArgumentException">The panel is not one of the host's.</exception>
    public void Toggle(IRulePanelModel panel)
    {
        Check(panel);
        SetCollapsed(panel, !_collapsed[panel]);
    }

    /// <summary>Sets the panel's state; raises <see cref="Changed"/> only when it differs.</summary>
    /// <exception cref="ArgumentException">The panel is not one of the host's.</exception>
    public void SetCollapsed(IRulePanelModel panel, bool collapsed)
    {
        Check(panel);
        if (_collapsed[panel] == collapsed)
        {
            return;
        }

        _collapsed[panel] = collapsed;
        Changed?.Invoke(this, panel);
    }

    /// <summary>Stops following the panels' check boxes. Safe to call twice.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (KeyValuePair<IRulePanelModel, EventHandler> pair in _handlers)
        {
            pair.Key.Changed -= pair.Value;
        }

        _handlers.Clear();
    }

    private void Check(IRulePanelModel panel)
    {
        if (panel == null || !_collapsed.ContainsKey(panel))
        {
            throw new ArgumentException("The panel is not one of the host's.", nameof(panel));
        }
    }

    private void OnPanelChanged(IRulePanelModel panel)
    {
        bool enabled = panel.Enabled;
        if (!_wasEnabled[panel] && enabled && _collapsed[panel])
        {
            SetCollapsed(panel, false);
        }

        _wasEnabled[panel] = enabled;
    }
}
