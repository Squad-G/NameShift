using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Ui.Instance;
using Xunit;

namespace NameShift.Ui.Tests.Instance;

/// <summary>NS-95: the owner window's own path arguments are the first request of the gatherer, so a cold start of
/// several processes ends like a start into a running window. A fake clock: no waiting, no pipe, no disk.</summary>
public sealed class StartGatheringTests
{
    private static readonly DateTime Start = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private DateTime _now = Start;

    private RequestGatherer NewGatherer() => new RequestGatherer(() => _now);

    private DateTime At(int milliseconds) => _now = Start.AddMilliseconds(milliseconds);

    private static InstanceRequest Piped(string cwd, string path) => new InstanceRequest(new[] { path }, cwd);

    [Fact]
    public void OwnerRequest_ArgsAndOwnDirectory()
    {
        InstanceRequest request = StartGathering.OwnerRequest(new[] { "-x", "a.jpg", "b.jpg" }, @"D:\cwd");

        Assert.Equal(new[] { "-x", "a.jpg", "b.jpg" }, request.Args);
        Assert.Equal(@"D:\cwd", request.CurrentDirectory);
        IReadOnlyList<PathArgument> paths = request.PathArguments();
        Assert.Equal(new[] { "a.jpg", "b.jpg" }, paths.Select(p => p.Text));
        Assert.All(paths, p => Assert.Equal(@"D:\cwd", p.CurrentDirectory));
    }

    [Fact]
    public void Begin_IsTheFirstRequest()
    {
        RequestGatherer gatherer = NewGatherer();

        Assert.True(StartGathering.Begin(gatherer, new[] { "a.jpg" }, @"D:\own"));
        At(100);
        gatherer.Add(Piped(@"D:\other", "b.jpg"));

        Assert.False(gatherer.IsDue(At(350)));
        Assert.True(gatherer.IsDue(At(400)));
        IReadOnlyList<PathArgument> paths = gatherer.Take();
        Assert.Equal(new[] { "a.jpg", "b.jpg" }, paths.Select(p => p.Text));
        Assert.Equal(new[] { @"D:\own", @"D:\other" }, paths.Select(p => p.CurrentDirectory));
    }

    [Fact]
    public void Begin_NoPath_False()
    {
        RequestGatherer gatherer = NewGatherer();

        Assert.False(StartGathering.Begin(gatherer, new string[0], @"D:\own"));
        Assert.False(StartGathering.Begin(gatherer, new[] { "-x", " " }, @"D:\own"));

        Assert.False(gatherer.HasPending);
    }

    [Fact]
    public void Begin_Cap2000()
    {
        RequestGatherer gatherer = NewGatherer();
        string[] many = Enumerable.Range(0, 2500).Select(i => @"D:\Pics\f" + i + ".jpg").ToArray();

        Assert.True(StartGathering.Begin(gatherer, many, @"D:\own"));

        IReadOnlyList<PathArgument> paths = gatherer.Take();
        Assert.Equal(2000, paths.Count);
        Assert.Equal(many[0], paths[0].Text);
        Assert.Equal(many[1999], paths[1999].Text);
    }

    [Fact]
    public void NullArguments_Throw()
    {
        RequestGatherer gatherer = NewGatherer();

        Assert.Throws<ArgumentNullException>(() => StartGathering.Begin(null!, new[] { "a" }, @"D:\own"));
        Assert.Throws<ArgumentNullException>(() => StartGathering.Begin(gatherer, null!, @"D:\own"));
        Assert.Throws<ArgumentNullException>(() => StartGathering.Begin(gatherer, new[] { "a" }, null!));
        Assert.Throws<ArgumentNullException>(() => StartGathering.OwnerRequest(null!, @"D:\own"));
        Assert.Throws<ArgumentNullException>(() => StartGathering.OwnerRequest(new[] { "a" }, null!));
    }

    [Fact]
    public void ColdStart15_OneBatch_OwnerIncluded()
    {
        RequestGatherer gatherer = NewGatherer();
        var source = new FakeFolderSource();
        string Name(int i) => "f" + i.ToString("00") + ".jpg";
        source.Files.Add(@"D:\Pics\" + Name(0));
        StartGathering.Begin(gatherer, new[] { @"D:\Pics\" + Name(0) }, @"D:\own");
        for (int i = 1; i <= 14; i++)
        {
            At(i * 50);
            source.Files.Add(@"D:\Pics\" + Name(i));
            gatherer.Add(Piped(@"D:\other", @"D:\Pics\" + Name(i)));
        }

        Assert.False(gatherer.IsDue(At(999)));
        Assert.True(gatherer.IsDue(At(1000)));
        IReadOnlyList<PathArgument> paths = gatherer.Take();
        Assert.Equal(15, paths.Count);
        Assert.Equal(@"D:\Pics\f00.jpg", paths[0].Text);
        MultiOpenRequest many = OpenTarget.ResolveMany(paths, source);
        Assert.Equal(@"D:\Pics", many.Folder);
        Assert.Equal(15, many.Names.Count);
        Assert.Equal(0, many.Rejected);
    }

    [Fact]
    public void SlowTrickle_CutAt3s_OwnerInFirstBatch()
    {
        RequestGatherer gatherer = NewGatherer();
        StartGathering.Begin(gatherer, new[] { @"D:\Pics\owner.jpg" }, @"D:\own");
        for (int ms = 250; ms <= 2750; ms += 250)
        {
            At(ms);
            gatherer.Add(Piped(@"D:\other", @"D:\Pics\p" + ms + ".jpg"));
            Assert.False(gatherer.IsDue(_now));
        }

        Assert.True(gatherer.IsDue(At(3000)));
        IReadOnlyList<PathArgument> paths = gatherer.Take();
        Assert.Equal(@"D:\Pics\owner.jpg", paths[0].Text);
        Assert.Equal(12, paths.Count);
    }

    [Fact]
    public void OnePath_DueOpenOne()
    {
        RequestGatherer gatherer = NewGatherer();
        StartGathering.Begin(gatherer, new[] { @"D:\Pics\a.jpg" }, @"D:\own");

        Assert.False(gatherer.IsDue(At(299)));
        Assert.True(gatherer.IsDue(At(300)));
        Assert.Equal(DueAction.OpenOne, RequestPlan.OnDue(gatherer.Take().Count, false, false));
    }

    [Fact]
    public void NotFound_EndsAsToday()
    {
        RequestGatherer gatherer = NewGatherer();
        StartGathering.Begin(gatherer, new[] { @"D:\Pics\missing.jpg" }, @"D:\own");
        At(300);
        Assert.True(gatherer.IsDue(_now));
        IReadOnlyList<PathArgument> paths = gatherer.Take();
        var source = new FakeFolderSource();

        OpenRequest request = OpenTarget.Resolve(paths[0].Text, paths[0].CurrentDirectory, source);
        MultiOpenRequest many = OpenTarget.ResolveMany(paths, source);

        Assert.Null(request.Folder);
        Assert.NotNull(request.Message);
        Assert.Null(many.Folder);
        Assert.NotNull(many.Message);
        var opens = new OpenSequence();
        int ticket = opens.Begin();
        Assert.Equal(OpenAction.OpenStartFolder, opens.Complete(ticket, many.Request, isBusy: false, folderShown: false));
    }

    [Fact]
    public void SeveralFolders_FirstWins()
    {
        RequestGatherer gatherer = NewGatherer();
        var source = new FakeFolderSource();
        source.Files.Add(@"D:\A\a.jpg");
        source.Files.Add(@"D:\B\b.jpg");
        StartGathering.Begin(gatherer, new[] { @"D:\A\a.jpg" }, @"D:\own");
        At(50);
        gatherer.Add(Piped(@"D:\other", @"D:\B\b.jpg"));

        MultiOpenRequest many = OpenTarget.ResolveMany(gatherer.Take(), source);

        Assert.Equal(@"D:\A", many.Folder);
        Assert.Equal(new[] { "a.jpg" }, many.Names);
        Assert.Equal(1, many.Rejected);
    }

    [Fact]
    public void LockedWindow_IsBusy()
    {
        Assert.Equal(DueAction.Busy, RequestPlan.OnDue(15, isBusy: true, dialogOpen: false));
    }
}
