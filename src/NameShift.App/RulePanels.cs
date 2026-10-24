using System.Collections.Generic;
using NameShift.App.Controls;
using NameShift.Ui.Rules;

namespace NameShift.App;

/// <summary>The composition point of the rule panels: a later rule adds a panel here without touching the host.</summary>
internal static class RulePanels
{
    /// <summary>Every implemented rule panel. A new rule panel adds its model and one entry here; the host orders them by stage.</summary>
    public static IReadOnlyList<RulePanelEntry> Create()
    {
        var replace = new ReplacePanelModel();
        var @case = new CasePanelModel();
        var remove = new RemovePanelModel();
        var add = new AddPanelModel();
        var numbering = new NumberingPanelModel();
        var name = new NamePanelModel();
        var extension = new ExtensionPanelModel();
        return new[]
        {
            new RulePanelEntry(replace, new ReplacePanel(replace)),
            new RulePanelEntry(@case, new CasePanel(@case)),
            new RulePanelEntry(remove, new RemovePanel(remove)),
            new RulePanelEntry(add, new AddPanel(add)),
            new RulePanelEntry(numbering, new NumberingPanel(numbering)),
            new RulePanelEntry(name, new NamePanel(name)),
            new RulePanelEntry(extension, new ExtensionPanel(extension)),
        };
    }
}
