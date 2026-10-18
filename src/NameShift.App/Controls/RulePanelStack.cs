using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NameShift.Ui;
using NameShift.Ui.Rules;

namespace NameShift.App.Controls;

/// <summary>The rule panels as a wrapping, vertically scrolling stack of frames in pipeline order.</summary>
internal sealed class RulePanelStack : UserControl
{
    public RulePanelStack(RulePanelHostModel host, IReadOnlyList<RulePanelEntry> entries)
    {
        if (entries.Any(e => !host.Panels.Contains(e.Model)))
        {
            throw new ArgumentException("An entry's model is not in the host.", nameof(entries));
        }

        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            AccessibleName = Strings.RulePanelsName,
        };

        for (int i = 0; i < host.Panels.Count; i++)
        {
            RulePanelEntry? entry = entries.FirstOrDefault(e => ReferenceEquals(e.Model, host.Panels[i]));
            if (entry == null)
            {
                throw new ArgumentException("A host panel has no entry.", nameof(entries));
            }

            flow.Controls.Add(new RulePanelFrame(entry.Model, entry.Content) { TabIndex = i });
        }

        Controls.Add(flow);
    }
}