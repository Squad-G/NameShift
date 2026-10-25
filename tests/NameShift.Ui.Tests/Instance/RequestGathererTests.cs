using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Ui.Instance;
using Xunit;

namespace NameShift.Ui.Tests.Instance;

/// <summary>NS-89 AC 3 and 5: a burst of requests is applied together after 300 ms of quiet, at most 3 s after the first;
/// a request without a path is not gathered. A fake clock: no waiting.</summary>
public sealed class RequestGathererTests
{
    private static readonly DateTime Start = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private DateTime _now = Start;

    private RequestGatherer NewGatherer() => new RequestGatherer(() => _now);

    private static InstanceRequest Req(string currentDirectory, params string[] args) => new InstanceRequest(args, currentDirectory);

    private DateTime At(int milliseconds) => _now = Start.AddMilliseconds(milliseconds);

    [Fact]
    public void Timings_Pinned()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(300), RequestGatherer.Quiet);
        Assert.Equal(TimeSpan.FromSeconds(3), RequestGatherer.Max);
    }

    [Fact]
    public void Empty_NeverDue()
    {
        RequestGatherer gatherer = NewGatherer();

        Assert.False(gatherer.HasPending);
        Assert.False(gatherer.IsDue(At(10_000)));
        Assert.Null(gatherer.NextCheck(_now));
        Assert.Empty(gatherer.Take());
    }

    [Fact]
    public void One()
    {
        RequestGatherer gatherer = NewGatherer();

        Assert.True(gatherer.Add(Req(@"C:\Work", @"D:\Pics\a.jpg")));

        Assert.True(gatherer.HasPending);
        Assert.False(gatherer.IsDue(At(299)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), gatherer.NextCheck(_now));
        Assert.True(gatherer.IsDue(At(300)));
        Assert.Equal(TimeSpan.Zero, gatherer.NextCheck(_now));
        PathArgument path = Assert.Single(gatherer.Take());
        Assert.Equal(@"D:\Pics\a.jpg", path.Text);
        Assert.Equal(@"C:\Work", path.CurrentDirectory);
        Assert.False(gatherer.HasPending);
        Assert.False(gatherer.IsDue(At(10_000)));
    }

    [Fact]
    public void Burst15_InsideQuiet()
    {
        RequestGatherer gatherer = NewGatherer();
        string[] names = Enumerable.Range(1, 15).Select(i => @"D:\Pics\p" + i + ".jpg").ToArray();

        for (int i = 0; i < names.Length; i++)
        {
            if (i > 0)
            {
                Assert.False(gatherer.IsDue(At((i * 50) - 1)));
            }

            At(i * 50);
            gatherer.Add(Req(@"C:\Work", names[i]));
            Assert.False(gatherer.IsDue(_now));
        }

        int last = 14 * 50;
        Assert.False(gatherer.IsDue(At(last + 299)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), gatherer.NextCheck(_now));
        Assert.True(gatherer.IsDue(At(last + 300)));
        Assert.Equal(names, gatherer.Take().Select(p => p.Text));
    }

    [Fact]
    public void Trickle_CutAt3s()
    {
        RequestGatherer gatherer = NewGatherer();

        for (int t = 0; t < 3000; t += 250)
        {
            Assert.False(gatherer.IsDue(At(t)));
            gatherer.Add(Req(@"C:\Work", "f" + t));
        }

        Assert.False(gatherer.IsDue(At(2999)));
        Assert.Equal(TimeSpan.FromMilliseconds(1), gatherer.NextCheck(_now)); // the cut, before the quiet end at 3050
        Assert.True(gatherer.IsDue(At(3000)));
        Assert.Equal(12, gatherer.Take().Count);
    }

    [Fact]
    public void AfterTake_TimesStartAgain()
    {
        RequestGatherer gatherer = NewGatherer();
        gatherer.Add(Req(@"C:\Work", "a"));
        At(2900);
        gatherer.Take();

        At(5000);
        gatherer.Add(Req(@"C:\Work", "b"));

        Assert.False(gatherer.IsDue(At(5299)));
        Assert.True(gatherer.IsDue(At(5300)));
    }

    [Fact]
    public void DifferentFolders_KeepOrder()
    {
        RequestGatherer gatherer = NewGatherer();
        gatherer.Add(Req(@"C:\Work", @"D:\First\a.jpg"));
        At(50);
        gatherer.Add(Req(@"C:\Work", @"E:\Second\b.jpg"));
        At(100);
        gatherer.Add(Req(@"C:\Work", @"D:\First\c.jpg"));
        At(400);

        IReadOnlyList<PathArgument> paths = gatherer.Take();

        Assert.Equal(new[] { @"D:\First\a.jpg", @"E:\Second\b.jpg", @"D:\First\c.jpg" }, paths.Select(p => p.Text));

        // NS-88: the first existing path's folder wins; the other folder's item is not selected.
        var source = new FakeFolderSource();
        source.Files.Add(@"D:\First\a.jpg");
        source.Files.Add(@"D:\First\c.jpg");
        source.Files.Add(@"E:\Second\b.jpg");
        MultiOpenRequest many = OpenTarget.ResolveMany(paths, source);
        Assert.Equal(@"D:\First", many.Folder);
        Assert.Equal(new[] { "a.jpg", "c.jpg" }, many.Names);
        Assert.Equal(1, many.Rejected);
        Assert.Equal(1, source.CheckCalls); // one disk check for the whole gathered request (AC 7)
    }

    [Fact]
    public void PerRequestCurrentDirectory()
    {
        RequestGatherer gatherer = NewGatherer();
        gatherer.Add(Req(@"C:\One", "a.jpg"));
        gatherer.Add(Req(@"C:\Two", "b.jpg", "c.jpg"));

        IReadOnlyList<PathArgument> paths = gatherer.Take();

        Assert.Equal(new[] { "a.jpg", "b.jpg", "c.jpg" }, paths.Select(p => p.Text));
        Assert.Equal(new[] { @"C:\One", @"C:\Two", @"C:\Two" }, paths.Select(p => p.CurrentDirectory));
    }

    [Fact]
    public void NoPath_NotGathered()
    {
        RequestGatherer gatherer = NewGatherer();

        Assert.False(gatherer.Add(Req(@"C:\Work")));
        Assert.False(gatherer.Add(Req(@"C:\Work", "-x", " ", "")));

        Assert.False(gatherer.HasPending);
        Assert.False(gatherer.IsDue(At(10_000)));
        Assert.Null(gatherer.NextCheck(_now));
    }

    [Fact]
    public void NoPath_DoesNotDelayOthers()
    {
        RequestGatherer gatherer = NewGatherer();
        gatherer.Add(Req(@"C:\Work", "a"));
        At(200);
        gatherer.Add(Req(@"C:\Work"));

        Assert.True(gatherer.IsDue(At(300))); // the request without a path did not restart the quiet time
    }

    [Fact]
    public void OptionsSkipped()
    {
        RequestGatherer gatherer = NewGatherer();
        gatherer.Add(Req(@"C:\Work", "-x", "a.jpg", " "));

        Assert.Equal(new[] { "a.jpg" }, gatherer.Take().Select(p => p.Text));
    }

    [Fact]
    public void Capped_AtMaxPaths()
    {
        RequestGatherer gatherer = NewGatherer();
        string[] many = Enumerable.Range(0, 2_000).Select(i => "f" + i).ToArray();
        gatherer.Add(Req(@"C:\Work", many));
        gatherer.Add(Req(@"C:\Work", "extra"));

        IReadOnlyList<PathArgument> paths = gatherer.Take();

        Assert.Equal(2_000, paths.Count);
        Assert.DoesNotContain(paths, p => p.Text == "extra");
    }

    [Fact]
    public void BadArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new RequestGatherer(null!));
        Assert.Throws<ArgumentNullException>(() => NewGatherer().Add(null!));
    }
}
