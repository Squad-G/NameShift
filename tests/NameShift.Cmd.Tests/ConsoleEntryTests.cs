using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace NameShift.Cmd.Tests;

/// <summary>The console seam: UTF-8 set before the writers are taken, restored afterwards, and never fatal without a
/// console.</summary>
public sealed class ConsoleEntryTests
{
    [Fact]
    public void ConsoleEntry_SetsUtf8_ThenTakesWriters_ThenRestores()
    {
        var events = new List<string>();
        Encoding old = Encoding.ASCII;
        var console = new FakeConsole(events, old);

        int code = ConsoleEntry.Run(
            console,
            () =>
            {
                events.Add("output");
                return new StringWriter();
            },
            () =>
            {
                events.Add("error");
                return new StringWriter();
            },
            (output, error) =>
            {
                events.Add("run");
                return 3;
            });

        Assert.Equal(3, code);
        Assert.Equal(new[] { "get", "set UTF-8", "output", "error", "run", "set " + old.WebName }, events);
        Assert.Equal(new[] { "set UTF-8", "set " + old.WebName }, console.Sets);
        Assert.Empty(console.Written[0].GetPreamble());
        Assert.Equal(Encoding.UTF8.CodePage, console.Written[0].CodePage);
        Assert.Same(old, console.Written[1]);
    }

    [Theory]
    [InlineData(true, false, false, 0)]
    [InlineData(false, true, false, 1)]
    [InlineData(true, true, false, 2)]
    [InlineData(false, false, true, 3)]
    [InlineData(true, true, true, 4)]
    public void ConsoleEntry_ThrowingSetAndRestore_Ignored(bool failFirstSet, bool failRestore, bool failGet, int exitCode)
    {
        var console = new FakeConsole(new List<string>(), Encoding.ASCII)
        {
            FailFirstSet = failFirstSet,
            FailRestore = failRestore,
            FailGet = failGet,
        };
        bool ran = false;

        int code = ConsoleEntry.Run(console, () => new StringWriter(), () => new StringWriter(), (output, error) =>
        {
            ran = true;
            return exitCode;
        });

        Assert.True(ran);
        Assert.Equal(exitCode, code);
    }

    [Fact]
    public void ConsoleEntry_ThrowingGet_DoesNotRestore()
    {
        var console = new FakeConsole(new List<string>(), Encoding.ASCII) { FailGet = true };

        ConsoleEntry.Run(console, () => new StringWriter(), () => new StringWriter(), (output, error) => 0);

        Assert.Single(console.Sets);
    }

    [Fact]
    public void ConsoleEntry_OtherException_Propagates()
    {
        var console = new FakeConsole(new List<string>(), Encoding.ASCII)
        {
            SetThrows = new InvalidOperationException("not an IOException"),
        };
        bool ran = false;

        Assert.Throws<InvalidOperationException>(() =>
            ConsoleEntry.Run(console, () => new StringWriter(), () => new StringWriter(), (output, error) =>
            {
                ran = true;
                return 0;
            }));

        Assert.False(ran);
    }

    [Fact]
    public void ConsoleEntry_RunThrows_StillRestores()
    {
        var events = new List<string>();
        var console = new FakeConsole(events, Encoding.ASCII);

        Assert.Throws<InvalidOperationException>(() =>
            ConsoleEntry.Run(console, () => new StringWriter(), () => new StringWriter(), (output, error) =>
                throw new InvalidOperationException("run")));

        Assert.Equal(2, console.Sets.Count);
    }

    private sealed class FakeConsole : IConsoleEncoding
    {
        private readonly List<string> _events;
        private Encoding _current;
        private int _sets;

        public FakeConsole(List<string> events, Encoding current)
        {
            _events = events;
            _current = current;
        }

        public bool FailFirstSet { get; set; }

        public bool FailRestore { get; set; }

        public bool FailGet { get; set; }

        public Exception? SetThrows { get; set; }

        public List<string> Sets { get; } = new List<string>();

        public List<Encoding> Written { get; } = new List<Encoding>();

        public Encoding OutputEncoding
        {
            get
            {
                _events.Add("get");
                if (FailGet)
                {
                    throw new IOException("The handle is invalid.");
                }

                return _current;
            }

            set
            {
                _sets++;
                string name = value.CodePage == Encoding.UTF8.CodePage ? "UTF-8" : value.WebName;
                _events.Add("set " + name);
                Sets.Add("set " + name);
                Written.Add(value);
                if (SetThrows != null)
                {
                    throw SetThrows;
                }

                if ((_sets == 1 && FailFirstSet) || (_sets > 1 && FailRestore))
                {
                    throw new IOException("The handle is invalid.");
                }

                _current = value;
            }
        }
    }
}
