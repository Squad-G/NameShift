using System;
using System.Collections.Generic;

namespace NameShift.App;

/// <summary>The composition point of the rule panels: a later rule adds a panel here without touching the host.</summary>
internal static class RulePanels
{
    /// <summary>Every implemented rule panel. NS-12, NS-53, NS-30 and NS-31 each add a line here.</summary>
    public static IReadOnlyList<RulePanelEntry> Create() => Array.Empty<RulePanelEntry>();
}