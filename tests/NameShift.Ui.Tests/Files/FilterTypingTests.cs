using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NameShift.Ui.Files;
using NameShift.Ui.Tests.Preview;
using Xunit;

namespace NameShift.Ui.Tests.Files;

/// <summary>NS-70 AC 3 and 9: the filter row's typing delay, with a fake delay (no real timer).</summary>
public sealed class FilterTypingTests
{
    private readonly FilterPanelModel _filters = new FilterPanelModel();
    private readonly FakeDelay _delay = new FakeDelay();
    private readonly FilterTyping _typing;
    private readonly List<string> _changes = new List<string>();
    private string _lastMask = "";
    private bool _lastIncludeFolders = true;
    private string _lastMax = "";

    public FilterTypingTests()
    {
        _typing = new FilterTyping(_filters, _delay.Delay);

        // Records which setting each Changed carried, in order (one setting changes per Changed here).
        _filters.Changed += (_, _) =>
        {
            lock (_changes)
            {
                if (_filters.Mask != _lastMask)
                {
                    _changes.Add("Mask");
                    _lastMask = _filters.Mask;
                }
                else if (_filters.IncludeFolders != _lastIncludeFolders)
                {
                    _changes.Add("IncludeFolders");
                    _lastIncludeFolders = _filters.IncludeFolders;
                }
                else if (_filters.MaxLengthText != _lastMax)
                {
                    _changes.Add("MaxLength");
                    _lastMax = _filters.MaxLengthText;
                }
                else
                {
                    _changes.Add("Other");
                }
            }
        };
    }

    private int ChangeCount
    {
        get
        {
            lock (_changes)
            {
                return _changes.Count;
            }
        }
    }

    private async Task ReleaseAllAndWait()
    {
        _delay.ReleaseAll();
        await TestWait.Within(_typing.PendingWait);
    }

    [Fact]
    public async Task TwoKeysInTheWait_OneSet_LastText()
    {
        _typing.Type(FilterField.Mask, "a");
        _typing.Type(FilterField.Mask, "ab");

        Assert.Equal(1, _delay.Cancelled);
        Assert.Equal("", _filters.Mask);
        Assert.True(_typing.IsPending(FilterField.Mask));

        await ReleaseAllAndWait();

        Assert.Equal("ab", _filters.Mask);
        Assert.Equal(1, ChangeCount);
        Assert.False(_typing.IsPending(FilterField.Mask));
    }

    [Fact]
    public void Delay_Is250ms()
    {
        _typing.Type(FilterField.Mask, "a");

        Assert.Equal(TimeSpan.FromMilliseconds(250), _delay.Calls[0]);
        Assert.Equal(FilterTyping.TypingDelay, _delay.Calls[0]);
    }

    [Fact]
    public async Task Flush_SetsAtOnce_WaitEndDoesNothing()
    {
        _typing.Type(FilterField.Pattern, "x");

        _typing.Flush();

        Assert.Equal("x", _filters.Pattern);
        Assert.Equal(1, ChangeCount);

        await ReleaseAllAndWait();

        Assert.Equal("x", _filters.Pattern);
        Assert.Equal(1, ChangeCount);
    }

    [Fact]
    public async Task CheckBoxAfterTyping_TextFirst()
    {
        _typing.Type(FilterField.Mask, "*.jpg");

        // What the row does for a check box: flush, then set.
        _typing.Flush();
        _filters.IncludeFolders = false;
        await ReleaseAllAndWait();

        Assert.Equal(new[] { "Mask", "IncludeFolders" }, _changes);
        Assert.Equal("*.jpg", _filters.Filter.Mask);
        Assert.False(_filters.Filter.IncludeFolders);
    }

    [Fact]
    public async Task Dispose_SetsNothing()
    {
        _typing.Type(FilterField.Mask, "a");

        _typing.Dispose();
        await ReleaseAllAndWait();

        Assert.Equal("", _filters.Mask);
        Assert.Equal(0, ChangeCount);
        Assert.False(_typing.IsPending(FilterField.Mask));

        _typing.Type(FilterField.Mask, "b");
        _typing.Flush();
        _typing.Dispose();

        Assert.Equal("", _filters.Mask);
        Assert.Equal(0, ChangeCount);
        Assert.Single(_delay.Calls);
    }

    [Fact]
    public async Task ExternalChange_DropsPending()
    {
        _typing.Type(FilterField.Mask, "a");

        _filters.IncludeHidden = true;

        Assert.False(_typing.IsPending(FilterField.Mask));
        await ReleaseAllAndWait();
        Assert.Equal("", _filters.Mask);
    }

    [Fact]
    public async Task Reset_DropsPending()
    {
        _filters.Pattern = "x";
        _typing.Type(FilterField.Pattern, "xy");

        _filters.Reset();
        await ReleaseAllAndWait();

        Assert.Equal("", _filters.Pattern);
        Assert.True(_filters.IsDefault);
    }

    [Fact]
    public void SeveralFields_FixedOrder()
    {
        _typing.Type(FilterField.MaxLength, "9");
        _typing.Type(FilterField.Mask, "a");

        _typing.Flush();

        Assert.Equal(new[] { "Mask", "MaxLength" }, _changes);
        Assert.Equal("a", _filters.Mask);
        Assert.Equal("9", _filters.MaxLengthText);
    }

    [Fact]
    public void IsPending_ClearedBeforeSet()
    {
        var seen = new List<bool>();
        _filters.Changed += (_, _) => seen.Add(_typing.IsPending(FilterField.Mask));
        _typing.Type(FilterField.Mask, "a");

        _typing.Flush();

        Assert.Equal(new[] { false }, seen);
    }

    [Fact]
    public void Null_IsEmpty()
    {
        _filters.Mask = "a";
        _typing.Type(FilterField.Mask, null!);

        _typing.Flush();

        Assert.Equal("", _filters.Mask);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new FilterTyping(null!));
        Assert.Throws<ArgumentNullException>(() => new FilterTyping(_filters, null!));
    }
}
