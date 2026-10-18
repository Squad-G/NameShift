using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NameShift.Core;

/// <summary>Applies the enabled rules, in order, to compute each file's new name.</summary>
public sealed class RenamePipeline(IReadOnlyList<IRenameRule> rules)
{
    public string NewName(string fileName, int index)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        stem = rules.Where(rule => rule.Enabled).Aggregate(stem, (current, rule) => rule.Apply(current, index));
        return stem + extension;
    }
}
