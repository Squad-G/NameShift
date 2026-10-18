using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NameShift.Disk;

/// <summary>Orders a batch's renames so that no move ever targets a name still in use: deepest folders first; in a
/// folder, a move whose target is free goes first, and a cycle (swap, rotation, case-only change) is broken through a
/// temporary name. Pure: it never touches the disk.</summary>
internal static class RenameOrder
{
    /// <summary>Plans the steps. folderKeys[i] is the normalized folder of renames[i]; newTempPath(folderKey) returns a
    /// free temporary full path in that folder. Old names and new names must each be unique per folder.</summary>
    internal static IReadOnlyList<RenameStep> Plan(
        IReadOnlyList<PlannedRename> renames, IReadOnlyList<string> folderKeys, Func<string, string> newTempPath)
    {
        var groups = Enumerable.Range(0, renames.Count)
            .GroupBy(i => folderKeys[i], StringComparer.OrdinalIgnoreCase)
            .Select(group => new { Key = group.Key, Indices = group.ToList() })
            .OrderByDescending(group => FolderKey.Depth(group.Key))
            .ThenBy(group => group.Indices[0]);

        var steps = new List<RenameStep>(renames.Count);
        foreach (var group in groups)
        {
            PlanFolder(renames, group.Key, group.Indices, newTempPath, steps);
        }

        return steps;
    }

    /// <summary>Plans one folder in O(n): a move is queued once its target is released by the move holding it.</summary>
    private static void PlanFolder(
        IReadOnlyList<PlannedRename> renames,
        string folderKey,
        List<int> indices,
        Func<string, string> newTempPath,
        List<RenameStep> steps)
    {
        var current = new Dictionary<int, string>();
        var holder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var waiter = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var i in indices)
        {
            current[i] = renames[i].OldName;
            holder[renames[i].OldName] = i;
            waiter[renames[i].NewName] = i;
        }

        var ready = new Queue<int>(indices.Where(i => !holder.ContainsKey(renames[i].NewName)));
        var done = new HashSet<int>();
        var nextInPlanOrder = 0;

        void Release(string name)
        {
            holder.Remove(name);
            if (waiter.TryGetValue(name, out var waiting))
            {
                waiter.Remove(name);
                ready.Enqueue(waiting);
            }
        }

        while (done.Count < indices.Count)
        {
            if (ready.Count > 0)
            {
                var i = ready.Dequeue();
                var from = current[i];
                steps.Add(new RenameStep(
                    i, Path.Combine(folderKey, from), Path.Combine(folderKey, renames[i].NewName), renames[i].Kind, isFinal: true));
                done.Add(i);
                Release(from);
                continue;
            }

            // Every remaining move waits for a name another remaining move holds: they are all in cycles.
            while (done.Contains(indices[nextInPlanOrder]))
            {
                nextInPlanOrder++;
            }

            var first = indices[nextInPlanOrder];
            var oldName = current[first];
            var tempPath = newTempPath(folderKey);
            steps.Add(new RenameStep(first, Path.Combine(folderKey, oldName), tempPath, renames[first].Kind, isFinal: false));
            current[first] = Path.GetFileName(tempPath);
            Release(oldName);
        }
    }
}
