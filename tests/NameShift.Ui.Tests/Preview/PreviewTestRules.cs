using System;
using System.Collections.Concurrent;
using System.Threading;
using NameShift.Core;

namespace NameShift.Ui.Tests.Preview;

/// <summary>A shared, thread-safe record of Apply calls: how many, and the setting each rule ran with.</summary>
internal sealed class ApplyCounter
{
    private readonly ConcurrentQueue<string> _texts = new ConcurrentQueue<string>();

    public int Count => _texts.Count;

    public string[] Texts => _texts.ToArray();

    public void Record(string text) => _texts.Enqueue(text);
}

/// <summary>Leaves every name unchanged and records each item's name, index and the item count it was given.</summary>
internal sealed class ContextRecordingRule : IRenameRule
{
    private readonly object _lock = new object();
    private readonly System.Collections.Generic.List<(string Name, int Index, int Count)> _calls = new System.Collections.Generic.List<(string, int, int)>();

    public RuleStage Stage => RuleStage.Replace;

    public bool Enabled => true;

    public System.Collections.Generic.List<(string Name, int Index, int Count)> Calls
    {
        get
        {
            lock (_lock)
            {
                return new System.Collections.Generic.List<(string, int, int)>(_calls);
            }
        }
    }

    public NameParts Apply(NameParts name, ItemContext context)
    {
        lock (_lock)
        {
            _calls.Add((context.Item.Name, context.Index, context.Count));
        }

        return name;
    }
}

/// <summary>Leaves every name unchanged and counts how often it ran.</summary>
internal sealed class CountingRule : IRenameRule
{
    private readonly ApplyCounter _counter;

    public CountingRule(RuleStage stage, string text, ApplyCounter counter)
    {
        Stage = stage;
        Text = text;
        _counter = counter;
    }

    /// <summary>The setting the rule was built with.</summary>
    public string Text { get; }

    public RuleStage Stage { get; }

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context)
    {
        _counter.Record(Text);
        return name;
    }
}

/// <summary>Blocks in Apply until the gate is set (only for the item named, or for every item), then changes nothing.</summary>
internal sealed class GateRule : IRenameRule
{
    private readonly ManualResetEventSlim _gate;
    private readonly string? _onlyItemName;

    public GateRule(RuleStage stage, ManualResetEventSlim gate, string? onlyItemName = null)
    {
        Stage = stage;
        _gate = gate;
        _onlyItemName = onlyItemName;
    }

    public RuleStage Stage { get; }

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (_onlyItemName == null || _onlyItemName == context.Item.Name)
        {
            _gate.Wait();
        }

        return name;
    }
}

/// <summary>Fails for one item index and passes the others.</summary>
internal sealed class ThrowingRule : IRenameRule
{
    private readonly int _failingIndex;
    private readonly string _message;

    public ThrowingRule(RuleStage stage, int failingIndex, string message)
    {
        Stage = stage;
        _failingIndex = failingIndex;
        _message = message;
    }

    public RuleStage Stage { get; }

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (context.Index == _failingIndex)
        {
            throw new InvalidOperationException(_message);
        }

        return name;
    }
}

/// <summary>Its Enabled getter throws: the pipeline reads it outside its per-item try, so the whole run fails.</summary>
internal sealed class BrokenRule : IRenameRule
{
    public BrokenRule(RuleStage stage)
    {
        Stage = stage;
    }

    public RuleStage Stage { get; }

    public bool Enabled => throw new InvalidOperationException("broken");

    public NameParts Apply(NameParts name, ItemContext context) => name;
}

/// <summary>Gives every item (or only those whose name starts with the given text) one fixed full name, after waiting
/// for the gate if there is one (for every renamed item, or only for the item named by gateItemName).</summary>
internal sealed class RenameToRule : IRenameRule
{
    private readonly string _fullName;
    private readonly string? _onlyItemNameStart;
    private readonly ManualResetEventSlim? _gate;
    private readonly string? _gateItemName;

    public RenameToRule(string fullName, string? onlyItemNameStart = null, ManualResetEventSlim? gate = null, string? gateItemName = null)
    {
        _fullName = fullName;
        _onlyItemNameStart = onlyItemNameStart;
        _gate = gate;
        _gateItemName = gateItemName;
    }

    /// <summary>Set when Apply starts waiting on the gate.</summary>
    public ManualResetEventSlim Waiting { get; } = new ManualResetEventSlim(false);

    public RuleStage Stage => RuleStage.Replace;

    public bool Enabled => true;

    public NameParts Apply(NameParts name, ItemContext context)
    {
        if (_onlyItemNameStart != null && !context.Item.Name.StartsWith(_onlyItemNameStart, StringComparison.Ordinal))
        {
            return name;
        }

        if (_gate != null && (_gateItemName == null || _gateItemName == context.Item.Name))
        {
            Waiting.Set();
            _gate.Wait();
        }

        return NameParts.Split(_fullName, context.Item.Kind);
    }
}
