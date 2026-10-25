using System;
using NameShift.Ui.Instance;
using Xunit;

namespace NameShift.Ui.Tests.Instance;

/// <summary>NS-89 AC 3, 4 and 5: the window's decisions on a request; a dialog counts as busy.</summary>
public sealed class RequestPlanTests
{
    private static readonly InstanceRequest WithPath = new InstanceRequest(new[] { "a.jpg" }, "C:");
    private static readonly InstanceRequest NoPath = new InstanceRequest(new[] { "-x", " " }, "C:");

    [Theory]
    [InlineData(true, false, false, ArrivalAction.Gather)]
    [InlineData(false, false, false, ArrivalAction.FrontOnly)]
    [InlineData(true, true, false, ArrivalAction.Busy)]
    [InlineData(false, true, false, ArrivalAction.Busy)]
    [InlineData(true, false, true, ArrivalAction.Busy)]
    [InlineData(false, false, true, ArrivalAction.Busy)]
    [InlineData(true, true, true, ArrivalAction.Busy)]
    public void OnArrival(bool hasPath, bool isBusy, bool dialogOpen, ArrivalAction expected)
    {
        Assert.Equal(expected, RequestPlan.OnArrival(hasPath ? WithPath : NoPath, isBusy, dialogOpen));
    }

    [Theory]
    [InlineData(0, false, false, DueAction.Nothing)]
    [InlineData(0, true, true, DueAction.Nothing)]
    [InlineData(1, false, false, DueAction.OpenOne)]
    [InlineData(2, false, false, DueAction.OpenMany)]
    [InlineData(15, false, false, DueAction.OpenMany)]
    [InlineData(1, true, false, DueAction.Busy)]
    [InlineData(15, true, false, DueAction.Busy)]
    [InlineData(1, false, true, DueAction.Busy)]
    [InlineData(15, false, true, DueAction.Busy)]
    public void OnDue(int pathCount, bool isBusy, bool dialogOpen, DueAction expected)
    {
        Assert.Equal(expected, RequestPlan.OnDue(pathCount, isBusy, dialogOpen));
    }

    [Fact]
    public void BadArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => RequestPlan.OnArrival(null!, false, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => RequestPlan.OnDue(-1, false, false));
    }
}
