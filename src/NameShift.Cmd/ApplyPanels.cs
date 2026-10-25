using System.Collections.Generic;
using NameShift.Ui.Rules;

namespace NameShift.Cmd;

/// <summary>The rule panels the command line filled, one window model per panel; null when no option of that panel was
/// given. Each given model has Enabled = true. Built by the parser only, then read-only by convention. NS-69 replaces a
/// preset's entry for a panel with the model here; NS-75 and NS-76 add their panels as new properties.</summary>
internal sealed class ApplyPanels
{
    public ApplyPanels(AddPanelModel? add, NumberingPanelModel? numbering)
    {
        Add = add;
        Numbering = numbering;
        var given = new List<IRulePanelModel>();
        if (add != null)
        {
            given.Add(add);
        }

        if (numbering != null)
        {
            given.Add(numbering);
        }

        Given = given.AsReadOnly();
    }

    /// <summary>No panel given.</summary>
    public static ApplyPanels None { get; } = new ApplyPanels(null, null);

    /// <summary>The Add panel, or null.</summary>
    public AddPanelModel? Add { get; }

    /// <summary>The Numbering panel, or null.</summary>
    public NumberingPanelModel? Numbering { get; }

    /// <summary>The given models, in stage order.</summary>
    public IReadOnlyList<IRulePanelModel> Given { get; }
}
