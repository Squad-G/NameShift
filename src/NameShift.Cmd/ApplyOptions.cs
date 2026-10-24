using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Rules;

namespace NameShift.Cmd;

/// <summary>The options of one <c>apply</c> command line, as parsed. Immutable.</summary>
internal sealed class ApplyOptions
{
    public ApplyOptions(string folderText, string? find, string? with, CaseMode? caseMode, bool dryRun)
    {
        FolderText = folderText ?? throw new ArgumentNullException(nameof(folderText));
        if ((find == null) != (with == null))
        {
            throw new ArgumentException("The find and with texts are given together or not at all.", nameof(with));
        }

        Find = find;
        With = with;
        Case = caseMode;
        DryRun = dryRun;
    }

    /// <summary>The folder, as typed.</summary>
    public string FolderText { get; }

    /// <summary>The text to find; null when --replace is absent.</summary>
    public string? Find { get; }

    /// <summary>The new text; null when --replace is absent.</summary>
    public string? With { get; }

    /// <summary>The case mode; null when --case is absent.</summary>
    public CaseMode? Case { get; }

    /// <summary>True with --dry-run: list the new names, rename nothing.</summary>
    public bool DryRun { get; }

    /// <summary>The rules, built by the window's own panel models, so the defaults are the window's by construction
    /// (Replace: ignore case, leave the extension; Case: no exceptions). The pipeline sorts them by stage.</summary>
    public IReadOnlyList<IRenameRule> BuildRules()
    {
        var rules = new List<IRenameRule>();
        if (Find != null)
        {
            IRenameRule? replace = new ReplacePanelModel { Find = Find, With = With! }.BuildRule();
            if (replace != null)
            {
                rules.Add(replace);
            }
        }

        if (Case.HasValue)
        {
            IRenameRule? caseRule = new CasePanelModel { Mode = Case.Value }.BuildRule();
            if (caseRule != null)
            {
                rules.Add(caseRule);
            }
        }

        return rules.AsReadOnly();
    }
}
