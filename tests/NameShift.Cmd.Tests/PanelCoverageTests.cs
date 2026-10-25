using System;
using System.Linq;
using System.Reflection;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>Every panel of the window has options on the command line, so a new panel cannot be forgotten there.</summary>
public sealed class PanelCoverageTests
{
    [Fact]
    public void EveryWindowPanel_HasCommandLineOptions()
    {
        Type[] panels = typeof(RulePanelModel).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(RulePanelModel).IsAssignableFrom(t))
            .ToArray();
        Type[] optionTypes = typeof(ApplyPanels).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.PropertyType)
            .ToArray();

        // Replace and Case are ApplyOptions fields (--replace and --case), not ApplyPanels properties.
        foreach (Type panel in panels)
        {
            bool covered = panel == typeof(ReplacePanelModel) || panel == typeof(CasePanelModel)
                || optionTypes.Contains(panel);
            Assert.True(covered, panel.Name + " has no command line options.");
        }

        Assert.Equal(11, panels.Length);
    }
}
