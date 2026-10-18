using System;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>
/// The contract every rule panel's settings model implements. A content control (NS-12 and later) takes its concrete
/// model, writes user input into it, and resyncs its fields from the model on <see cref="Changed"/> (setting a
/// control's value only when it differs, to avoid loops): that is how Reset reaches the controls. An invalid field is
/// the panel's business: <see cref="BuildRule"/> returns null or a rule without that part, it never throws.
/// </summary>
public interface IRulePanelModel
{
    /// <summary>The panel's place in the pipeline; one panel per stage. Also its stable key for presets (NS-36).</summary>
    RuleStage Stage { get; }

    /// <summary>The visible title from Strings, may carry one '&amp;' mnemonic (e.g. "&amp;Replace").</summary>
    string Title { get; }

    /// <summary>The enable checkbox. Setting a different value raises <see cref="Changed"/>.</summary>
    bool Enabled { get; set; }

    /// <summary>True when every setting equals its default (Enabled is not a setting).</summary>
    bool IsDefault { get; }

    /// <summary>Puts every setting back to its default and unchecks the panel; raises <see cref="Changed"/> once.</summary>
    void Reset();

    /// <summary>The Core rule for the current settings, or null when the settings give nothing to apply
    /// (e.g. an invalid required field). Never throws. Its Stage must equal this panel's Stage.</summary>
    IRenameRule? BuildRule();

    /// <summary>Raised on the calling (UI) thread, synchronously, after any setting or Enabled changes.</summary>
    event EventHandler? Changed;
}