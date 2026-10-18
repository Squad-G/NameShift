using System;
using System.Collections.Generic;

namespace NameShift.Core.Tests;

internal sealed class DelegateRule(
    RuleStage stage,
    Func<NameParts, ItemContext, NameParts> apply,
    bool enabled = true) : IRenameRule
{
    public RuleStage Stage => stage;

    public bool Enabled => enabled;

    public NameParts Apply(NameParts name, ItemContext context) => apply(name, context);
}

internal sealed class RecordingRule(RuleStage stage = RuleStage.Name) : IRenameRule
{
    public List<(NameParts Name, ItemContext Context)> Calls { get; } = [];

    public RuleStage Stage => stage;

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context)
    {
        Calls.Add((name, context));
        return name;
    }
}

internal sealed class ThrowingRule(RuleStage stage, int failAtIndex, string message) : IRenameRule
{
    public RuleStage Stage => stage;

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context) =>
        context.Index == failAtIndex ? throw new InvalidOperationException(message) : name;
}

internal static class Items
{
    public static RenameItem File(string name) => new(name, @"C:\Test");

    public static RenameItem[] Files(params string[] names) => Array.ConvertAll(names, File);
}
