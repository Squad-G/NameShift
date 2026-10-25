using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NameShift.App.Controls;
using NameShift.Ui.Rules;

namespace NameShift.App;

/// <summary>The composition point of the rule panels: the models come from <see cref="RulePanelModels"/> (shared with
/// the command line) and each gets its control here.</summary>
internal static class RulePanels
{
    /// <summary>Every implemented rule panel, in the factory's order; the host orders them by stage. A new panel adds
    /// its model to <see cref="RulePanelModels.CreateAll"/> and its control to <see cref="ContentFor"/>.</summary>
    public static IReadOnlyList<RulePanelEntry> Create() =>
        RulePanelModels.CreateAll().Select(model => new RulePanelEntry(model, ContentFor(model))).ToList();

    private static Control ContentFor(IRulePanelModel model) => model switch
    {
        ReplacePanelModel m => new ReplacePanel(m),
        CasePanelModel m => new CasePanel(m),
        RemovePanelModel m => new RemovePanel(m),
        AddPanelModel m => new AddPanel(m),
        NumberingPanelModel m => new NumberingPanel(m),
        NamePanelModel m => new NamePanel(m),
        ExtensionPanelModel m => new ExtensionPanel(m),
        RegexPanelModel m => new RegexPanel(m),
        MoveCopyPanelModel m => new MoveCopyPanel(m),
        AutoDatePanelModel m => new AutoDatePanel(m),
        FolderNamePanelModel m => new FolderNamePanel(m),
        _ => throw new InvalidOperationException("No panel control for " + model.GetType().Name + "."),
    };
}
