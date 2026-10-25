using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Ui.Rules;

namespace NameShift.Cmd;

/// <summary>The rule panels the command line filled, one window model per panel, in stage order. Each given model has
/// Enabled = true. Built by the parser only, then read-only by convention. NS-69 replaces a preset's entry for a panel
/// with the model here. Each panel has a property, so a test can check that every window panel has options.</summary>
internal sealed class ApplyPanels
{
    public ApplyPanels(IEnumerable<IRulePanelModel> models)
    {
        if (models == null)
        {
            throw new ArgumentNullException(nameof(models));
        }

        var given = new List<IRulePanelModel>();
        foreach (IRulePanelModel model in models)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(models), "A model is null.");
            }

            if (given.Any(g => g.Stage == model.Stage))
            {
                throw new ArgumentException("Two models of the stage " + model.Stage + ".", nameof(models));
            }

            given.Add(model);
        }

        Given = given.OrderBy(m => (int)m.Stage).ToList().AsReadOnly();
    }

    /// <summary>No panel given.</summary>
    public static ApplyPanels None { get; } = new ApplyPanels(new List<IRulePanelModel>());

    /// <summary>The Add panel, or null.</summary>
    public AddPanelModel? Add => Given.OfType<AddPanelModel>().FirstOrDefault();

    /// <summary>The Numbering panel, or null.</summary>
    public NumberingPanelModel? Numbering => Given.OfType<NumberingPanelModel>().FirstOrDefault();

    /// <summary>The Name panel, or null.</summary>
    public NamePanelModel? Name => Given.OfType<NamePanelModel>().FirstOrDefault();

    /// <summary>The Extension panel, or null.</summary>
    public ExtensionPanelModel? Extension => Given.OfType<ExtensionPanelModel>().FirstOrDefault();

    /// <summary>The Remove panel, or null.</summary>
    public RemovePanelModel? Remove => Given.OfType<RemovePanelModel>().FirstOrDefault();

    /// <summary>The RegEx panel, or null.</summary>
    public RegexPanelModel? Regex => Given.OfType<RegexPanelModel>().FirstOrDefault();

    /// <summary>The Move or copy panel, or null.</summary>
    public MoveCopyPanelModel? MoveCopy => Given.OfType<MoveCopyPanelModel>().FirstOrDefault();

    /// <summary>The Auto date panel, or null.</summary>
    public AutoDatePanelModel? AutoDate => Given.OfType<AutoDatePanelModel>().FirstOrDefault();

    /// <summary>The Append folder name panel, or null.</summary>
    public FolderNamePanelModel? FolderName => Given.OfType<FolderNamePanelModel>().FirstOrDefault();

    /// <summary>The given models, in stage order.</summary>
    public IReadOnlyList<IRulePanelModel> Given { get; }
}
