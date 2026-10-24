using System;
using System.Collections.Generic;

namespace NameShift.Core;

/// <summary>Applies several Remove rules as one stage-5 rule: each enabled part in the given order, each on the
/// result of the previous one. The group never reorders its parts. The window builds it from the Remove panel.</summary>
public sealed class RemoveGroupRule : IRenameRule
{
    private readonly IRenameRule[] _parts;

    /// <exception cref="ArgumentNullException">parts is null.</exception>
    /// <exception cref="ArgumentException">A part is null, or its stage is not Remove.</exception>
    public RemoveGroupRule(IEnumerable<IRenameRule> parts)
    {
        if (parts == null)
        {
            throw new ArgumentNullException(nameof(parts));
        }

        _parts = new List<IRenameRule>(parts).ToArray();
        foreach (IRenameRule part in _parts)
        {
            if (part == null)
            {
                throw new ArgumentException("A part is null.", nameof(parts));
            }

            if (part.Stage != RuleStage.Remove)
            {
                throw new ArgumentException("A part is not a Remove rule.", nameof(parts));
            }
        }

        Parts = Array.AsReadOnly(_parts);
    }

    /// <summary>The parts in the order given.</summary>
    public IReadOnlyList<IRenameRule> Parts { get; }

    public RuleStage Stage => RuleStage.Remove;

    /// <summary>True when at least one part is enabled.</summary>
    public bool Enabled
    {
        get
        {
            foreach (IRenameRule part in _parts)
            {
                if (part.Enabled)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public NameParts Apply(NameParts name, ItemContext context)
    {
        foreach (IRenameRule part in _parts)
        {
            if (part.Enabled)
            {
                name = part.Apply(name, context);
            }
        }

        return name;
    }
}
