using System.Collections.Generic;

namespace NameShift.Ui.Rules;

/// <summary>The rule panels of the window, in the window's order: the window and the command line both build their
/// panels here, so a preset means the same in both and a new panel cannot be missing from one side.</summary>
public static class RulePanelModels
{
    /// <summary>A fresh model of every rule panel (default settings, unchecked), in the window's order. New instances on
    /// each call.</summary>
    public static IReadOnlyList<IRulePanelModel> CreateAll() => new IRulePanelModel[]
    {
        new ReplacePanelModel(),
        new CasePanelModel(),
        new RemovePanelModel(),
        new AddPanelModel(),
        new NumberingPanelModel(),
        new NamePanelModel(),
        new ExtensionPanelModel(),
        new RegexPanelModel(),
        new MoveCopyPanelModel(),
        new AutoDatePanelModel(),
        new FolderNamePanelModel(),
    };
}
