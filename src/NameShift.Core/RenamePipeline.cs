using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NameShift.Core;

/// <summary>Runs the enabled rules in stage order to compute each item's new name, without touching the disk.</summary>
public sealed class RenamePipeline
{
    /// <summary>Creates a pipeline; the rules are sorted by stage, keeping registration order within a stage.</summary>
    public RenamePipeline(IEnumerable<IRenameRule> rules)
    {
        if (rules == null)
        {
            throw new ArgumentNullException(nameof(rules));
        }

        var list = rules.ToArray();
        foreach (var rule in list)
        {
            if (rule == null)
            {
                throw new ArgumentException("A rule is null.", nameof(rules));
            }

            if (!Enum.IsDefined(typeof(RuleStage), rule.Stage))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rules),
                    $"{rule.GetType().Name} has a stage outside {MinStage} to {MaxStage}.");
            }
        }

        Rules = Array.AsReadOnly(list.OrderBy(rule => (int)rule.Stage).ToArray());
    }

    private static readonly int MinStage = Enum.GetValues(typeof(RuleStage)).Cast<int>().Min();

    private static readonly int MaxStage = Enum.GetValues(typeof(RuleStage)).Cast<int>().Max();

    /// <summary>The rules in run order.</summary>
    public IReadOnlyList<IRenameRule> Rules { get; }

    /// <summary>Computes one result per item, in input order; each item's context carries its folder index.</summary>
    public IReadOnlyList<RenameResult> Run(
        IReadOnlyList<RenameItem> items,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }

        // Enabled is read once here, at run time: rules must not change while a run is in progress.
        var active = Rules.Where(rule => rule.Enabled).ToArray();
        var results = new RenameResult[items.Count];
        var perFolder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = items[i];
            if (item == null)
            {
                throw new ArgumentException("An item is null.", nameof(items));
            }

            var key = item.FolderPath.TrimEnd('\\', '/');
            perFolder.TryGetValue(key, out var folderIndex);
            perFolder[key] = folderIndex + 1;
            results[i] = Compute(active, item, new ItemContext(item, i, items.Count, folderIndex));
        }

        return results;
    }

    private static RenameResult Compute(IRenameRule[] active, RenameItem item, ItemContext context)
    {
        var parts = NameParts.Split(item.Name, item.Kind);
        foreach (var rule in active)
        {
            try
            {
                parts = rule.Apply(parts, context) ?? throw new InvalidOperationException("The rule returned no name.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var error = new RenameError(rule.Stage, rule.GetType().Name, ex.Message);
                return new RenameResult(item, context.Index, item.Name, RenameStatus.Error, error);
            }
        }

        var newName = parts.ToFullName();
        var status = string.Equals(newName, item.Name, StringComparison.Ordinal)
            ? RenameStatus.Unchanged
            : RenameStatus.Changed;
        return new RenameResult(item, context.Index, newName, status, null);
    }
}
