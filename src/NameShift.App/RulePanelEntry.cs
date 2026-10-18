using System.Windows.Forms;
using NameShift.Ui.Rules;

namespace NameShift.App;

/// <summary>One rule panel: its settings model and its content control.</summary>
internal sealed class RulePanelEntry
{
    public RulePanelEntry(IRulePanelModel model, Control content)
    {
        Model = model;
        Content = content;
    }

    public IRulePanelModel Model { get; }

    public Control Content { get; }
}