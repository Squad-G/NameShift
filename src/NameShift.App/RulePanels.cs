using System.Collections.Generic;
using NameShift.App.Controls;
using NameShift.Ui.Rules;

namespace NameShift.App;

/// <summary>The composition point of the rule panels: a later rule adds a panel here without touching the host.</summary>
internal static class RulePanels
{
    /// <summary>Every implemented rule panel. NS-53, NS-30 and NS-31 each add a line here.</summary>
    public static IReadOnlyList<RulePanelEntry> Create()
    {
        var replace = new ReplacePanelModel();
        var @case = new CasePanelModel();
        var add = new AddPanelModel();
        return new[]
        {
            new RulePanelEntry(replace, new ReplacePanel(replace)),
            new RulePanelEntry(@case, new CasePanel(@case)),
            new RulePanelEntry(add, new AddPanel(add)),
        };
    }
}
