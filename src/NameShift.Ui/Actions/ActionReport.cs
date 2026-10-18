using System;

namespace NameShift.Ui.Actions;

/// <summary>What the window says after an action: one status-bar line, and a list for a dialog only when the user must
/// look at specific items.</summary>
public sealed class ActionReport
{
    public ActionReport(string status, string? details = null)
    {
        Status = status ?? throw new ArgumentNullException(nameof(status));
        Details = details;
    }

    /// <summary>The status-bar line.</summary>
    public string Status { get; }

    /// <summary>Null for every routine result (success included); lines joined with Environment.NewLine.</summary>
    public string? Details { get; }
}
