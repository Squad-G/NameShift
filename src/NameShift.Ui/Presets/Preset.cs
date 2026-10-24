using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Ui.Rules;

namespace NameShift.Ui.Presets;

/// <summary>A saved set of rules: a name and the panels that are enabled or not at default. Rules only: no filter,
/// folder or selection. Immutable.</summary>
public sealed class Preset
{
    /// <summary>Keeps the panels in stage order.</summary>
    /// <exception cref="ArgumentNullException">The name, the panels or one panel is null.</exception>
    /// <exception cref="ArgumentException">Two panels have the same stage.</exception>
    public Preset(string name, IEnumerable<PresetPanel> panels)
    {
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (panels == null)
        {
            throw new ArgumentNullException(nameof(panels));
        }

        var list = panels.ToList();
        if (list.Any(p => p == null))
        {
            throw new ArgumentNullException(nameof(panels), "A panel is null.");
        }

        if (list.Select(p => p.Stage).Distinct().Count() != list.Count)
        {
            throw new ArgumentException("Two panels have the same stage.", nameof(panels));
        }

        Name = name;
        Panels = list.OrderBy(p => (int)p.Stage).ToList().AsReadOnly();
    }

    /// <summary>The name as typed (the store refuses a blank one, this type does not).</summary>
    public string Name { get; }

    /// <summary>The panels in stage order, one per stage at most.</summary>
    public IReadOnlyList<PresetPanel> Panels { get; }

    /// <summary>A preset of the panels that are enabled or have a non-default setting, each with its settings and its
    /// Enabled.</summary>
    /// <exception cref="ArgumentNullException">The name, the models or one model is null.</exception>
    /// <exception cref="ArgumentException">Two models have the same stage.</exception>
    public static Preset FromPanels(string name, IEnumerable<IRulePanelModel> models)
    {
        if (models == null)
        {
            throw new ArgumentNullException(nameof(models));
        }

        var panels = new List<PresetPanel>();
        foreach (IRulePanelModel model in models)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(models), "A model is null.");
            }

            if (model.Enabled || !model.IsDefault)
            {
                panels.Add(new PresetPanel(model.Stage, model.Enabled, model.SaveSettings()));
            }
        }

        return new Preset(name, panels);
    }
}
