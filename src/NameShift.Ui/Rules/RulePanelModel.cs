using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The plumbing of a rule panel's model, so a concrete panel only holds its settings (through SetField).</summary>
public abstract class RulePanelModel : IRulePanelModel
{
    private bool _enabled;
    private bool _suppressed;

    protected RulePanelModel(RuleStage stage, string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            throw new ArgumentException("The title is required.", nameof(title));
        }

        Stage = stage;
        Title = title;
    }

    public event EventHandler? Changed;

    public RuleStage Stage { get; }

    public string Title { get; }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value)
            {
                return;
            }

            _enabled = value;
            OnChanged();
        }
    }

    public abstract bool IsDefault { get; }

    public abstract IRenameRule? BuildRule();

    /// <summary>Puts every setting back to its default and unchecks the panel; raises Changed exactly once.</summary>
    public void Reset()
    {
        _suppressed = true;
        try
        {
            ResetSettings();
            Enabled = false;
        }
        finally
        {
            _suppressed = false;
        }

        // Raised even if nothing changed: the frame and the content resync either way.
        OnChanged();
    }

    /// <summary>Puts every setting back to its default, through SetField.</summary>
    protected abstract void ResetSettings();

    /// <summary>Assigns the field and raises Changed when the value differs; returns whether it did.</summary>
    protected bool SetField<T>(ref T field, T value)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnChanged();
        return true;
    }

    /// <summary>Raises Changed (not during Reset), for a panel that changes several fields at once.</summary>
    protected void OnChanged()
    {
        if (!_suppressed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}