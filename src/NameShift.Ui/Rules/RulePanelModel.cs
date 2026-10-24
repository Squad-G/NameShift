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

    /// <inheritdoc />
    public abstract IReadOnlyDictionary<string, string> SaveSettings();

    /// <inheritdoc />
    public void LoadSettings(IReadOnlyDictionary<string, string> settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        _suppressed = true;
        try
        {
            ResetSettings();
            ApplySettings(settings);
        }
        finally
        {
            _suppressed = false;
        }

        // Once, as Reset does; Enabled is not touched.
        OnChanged();
    }

    /// <summary>Puts every setting back to its default, through SetField.</summary>
    protected abstract void ResetSettings();

    /// <summary>Applies the known keys of the map onto the defaults, in the model's own order, through the setters.
    /// A value the model does not accept leaves that setting at its default; never throws.</summary>
    protected abstract void ApplySettings(IReadOnlyDictionary<string, string> settings);

    /// <summary>A bool as saved in a preset: "true" or "false".</summary>
    protected static string BoolText(bool value) => value ? "true" : "false";

    /// <summary>The text under the key (a null value reads as ""); false when the key is missing.</summary>
    protected static bool TryGetText(IReadOnlyDictionary<string, string> settings, string key, out string value)
    {
        if (settings.TryGetValue(key, out string? text))
        {
            value = text ?? "";
            return true;
        }

        value = "";
        return false;
    }

    /// <summary>The bool under the key: "true" or "false", ignoring case; false for a missing key or any other text.</summary>
    protected static bool TryGetBool(IReadOnlyDictionary<string, string> settings, string key, out bool value)
    {
        value = false;
        if (!settings.TryGetValue(key, out string? text) || text == null)
        {
            return false;
        }

        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        return string.Equals(text, "false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The enum under the key, by the name of a defined value only (ignoring case): a number ("3") or a list
    /// ("1, 2"), which Enum.TryParse would take, is refused. False for a missing key or any other text.</summary>
    protected static bool TryGetEnum<T>(IReadOnlyDictionary<string, string> settings, string key, out T value)
        where T : struct, Enum
    {
        value = default;
        if (!settings.TryGetValue(key, out string? text) || text == null)
        {
            return false;
        }

        foreach (string name in Enum.GetNames(typeof(T)))
        {
            if (string.Equals(name, text, StringComparison.OrdinalIgnoreCase))
            {
                value = (T)Enum.Parse(typeof(T), name);
                return true;
            }
        }

        return false;
    }

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
