using System;
using NameShift.Ui.Settings;
using Xunit;

namespace NameShift.Ui.Tests.Settings;

/// <summary>NS-37 AC 5, 7: where a saved window opens again. Positions in device pixels, saved sizes logical.</summary>
public sealed class WindowPlacementTests
{
    private const int MinWidth = 760;
    private const int MinHeight = 520;

    private static readonly ScreenArea Primary = new ScreenArea(0, 0, 1920, 1040, 1.0);
    private static readonly ScreenArea LeftScreen = new ScreenArea(-1920, 0, 1920, 1040, 1.0);
    private static readonly ScreenArea Wide150 = new ScreenArea(1920, 0, 2560, 1400, 1.5);

    private static PlacedWindow? Fit(SavedWindow saved, params ScreenArea[] screens) =>
        WindowPlacement.Fit(saved, screens, MinWidth, MinHeight);

    private static void AssertRectangle(PlacedWindow? placed, int left, int top, int width, int height)
    {
        Assert.NotNull(placed);
        Assert.Equal(left, placed!.Left);
        Assert.Equal(top, placed.Top);
        Assert.Equal(width, placed.Width);
        Assert.Equal(height, placed.Height);
    }

    [Fact]
    public void OnScreen_SameRectangle()
    {
        PlacedWindow? placed = Fit(new SavedWindow(100, 100, 1100, 720, false), Primary);

        AssertRectangle(placed, 100, 100, 1100, 720);
        Assert.False(placed!.Maximized);
        Assert.Equal(1.0, placed.Scale);
    }

    [Fact]
    public void PartlyOff_ButGrabbable()
    {
        // The strip has 1920 - 1700 = 220 device px on the screen: the position is kept.
        AssertRectangle(Fit(new SavedWindow(1700, 200, 1100, 720, false), Primary), 1700, 200, 1100, 720);
    }

    [Fact]
    public void StripJustWideEnough_Placed_OneLess_NoPlacement()
    {
        Assert.NotNull(Fit(new SavedWindow(1820, 200, 1100, 720, false), Primary));
        Assert.Null(Fit(new SavedWindow(1821, 200, 1100, 720, false), Primary));
    }

    [Fact]
    public void StripTooNarrow_NoPlacement()
    {
        Assert.Null(Fit(new SavedWindow(1850, 200, 1100, 720, false), Primary));
    }

    [Fact]
    public void TitleBarAboveTheScreen_NoPlacement()
    {
        // The window's lower part overlaps the screen (its size is raised to the minimum), but its title-bar band does not.
        Assert.Null(Fit(new SavedWindow(100, -100, 1100, 50, false), Primary));
        Assert.Null(Fit(new SavedWindow(100, -40, 1100, 720, false), Primary));
        Assert.NotNull(Fit(new SavedWindow(100, -39, 1100, 720, false), Primary));
    }

    [Fact]
    public void FullyOff_NoPlacement()
    {
        Assert.Null(Fit(new SavedWindow(5000, 5000, 1100, 720, false), Primary, LeftScreen));
    }

    [Fact]
    public void FarAway_NoOverflow()
    {
        Assert.Null(Fit(new SavedWindow(int.MaxValue - 10, int.MinValue + 10, 1100, 720, false), Primary));
    }

    [Fact]
    public void SecondScreenGone_NoPlacement()
    {
        Assert.Null(Fit(new SavedWindow(-1800, 100, 1100, 720, false), Primary));
    }

    [Fact]
    public void NoScreens_NoPlacement()
    {
        Assert.Null(Fit(new SavedWindow(100, 100, 1100, 720, false)));
    }

    [Fact]
    public void TooSmall_RaisedToMinimum()
    {
        AssertRectangle(Fit(new SavedWindow(100, 100, 300, 200, false), Primary), 100, 100, 760, 520);
    }

    [Fact]
    public void TooSmall_RaisedToMinimum_AtTheLandingScale()
    {
        AssertRectangle(Fit(new SavedWindow(2000, 100, 300, 200, false), Wide150), 2000, 100, 1140, 780);
    }

    [Fact]
    public void LargerThanScreen_CutToWorkingArea()
    {
        AssertRectangle(Fit(new SavedWindow(0, 0, 4000, 3000, false), Primary), 0, 0, 1920, 1040);
    }

    [Fact]
    public void WorkingAreaSmallerThanMinimum_WorkingAreaWins()
    {
        var small = new ScreenArea(0, 0, 640, 480, 1.0);

        AssertRectangle(Fit(new SavedWindow(0, 0, 300, 200, false), small), 0, 0, 640, 480);
    }

    [Fact]
    public void NegativeCoordinates_LeftScreen()
    {
        PlacedWindow? placed = Fit(new SavedWindow(-1800, 100, 1100, 720, false), Primary, LeftScreen);

        AssertRectangle(placed, -1800, 100, 1100, 720);
        Assert.Equal(1.0, placed!.Scale);
    }

    [Fact]
    public void Saved150_Now100()
    {
        SavedWindow saved = WindowPlacement.ToSaved(0, 0, 1650, 1080, 1.5, false);

        Assert.Equal(1100, saved.Width);
        Assert.Equal(720, saved.Height);
        AssertRectangle(Fit(saved, Primary), 0, 0, 1100, 720);
    }

    [Fact]
    public void Saved100_Lands150()
    {
        PlacedWindow? placed = Fit(new SavedWindow(2000, 100, 1100, 720, false), Primary, Wide150);

        AssertRectangle(placed, 2000, 100, 1650, 1080);
        Assert.Equal(1.5, placed!.Scale);
    }

    [Fact]
    public void Maximized_KeepsRestoreBounds()
    {
        PlacedWindow? placed = Fit(new SavedWindow(100, 100, 1100, 720, true), Primary);

        AssertRectangle(placed, 100, 100, 1100, 720);
        Assert.True(placed!.Maximized);
    }

    [Fact]
    public void LandingScreen_LargestOverlap()
    {
        // All of it on the primary (1100 x 720); at 1.5 only 230 x 1080 px of it reach the right screen.
        PlacedWindow? placed = Fit(new SavedWindow(500, 100, 1100, 720, false), Primary, Wide150);

        Assert.Equal(1.0, placed!.Scale);
        Assert.Equal(1100, placed.Width);
    }

    [Fact]
    public void LandingScreen_LargestOverlap_RightScreen()
    {
        // Mostly on the 150 % screen: it lands there and takes its scale.
        PlacedWindow? placed = Fit(new SavedWindow(1800, 100, 1100, 720, false), Primary, Wide150);

        Assert.Equal(1.5, placed!.Scale);
        Assert.Equal(1650, placed.Width);
    }

    [Fact]
    public void Tie_FirstListed()
    {
        // The window has 500 x 600 px on each screen. The right screen is only 900 px wide, so the width tells which
        // screen it landed on: 1000 on the left one, cut to 900 on the right one.
        var left = new ScreenArea(0, 0, 1000, 1000, 1.0);
        var right = new ScreenArea(1000, 0, 900, 1000, 1.0);
        var saved = new SavedWindow(500, 0, 1000, 600, false);

        Assert.Equal(1000, WindowPlacement.Fit(saved, new[] { left, right }, 100, 100)!.Width);
        Assert.Equal(900, WindowPlacement.Fit(saved, new[] { right, left }, 100, 100)!.Width);
    }

    [Fact]
    public void ToSaved_RoundsAwayFromZero()
    {
        Assert.Equal(1101, WindowPlacement.ToSaved(0, 0, 1651, 100, 1.5, false).Width);
        Assert.Equal(165, WindowPlacement.ToSaved(0, 0, 165, 100, 1.0, false).Width);
        Assert.Equal(2, WindowPlacement.ToSaved(0, 0, 3, 3, 2.0, false).Width);
        Assert.Equal(2, WindowPlacement.ToSaved(0, 0, 3, 3, 2.0, false).Height);
    }

    [Fact]
    public void ToSaved_KeepsPositionAndFlag()
    {
        SavedWindow saved = WindowPlacement.ToSaved(-1650, -8, 1650, 1080, 1.5, true);

        Assert.Equal(-1650, saved.Left);
        Assert.Equal(-8, saved.Top);
        Assert.True(saved.Maximized);
    }

    [Fact]
    public void InvalidArguments_Throw()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedWindow(0, 0, 0, 100, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SavedWindow(0, 0, 100, -1, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenArea(0, 0, 100, 100, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenArea(0, 0, 100, 100, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenArea(0, 0, 0, 100, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowPlacement.ToSaved(0, 0, 100, 100, 0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowPlacement.ToSaved(0, 0, 0, 100, 1.0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => WindowPlacement.ToSaved(0, 0, 100, 0, 1.0, false));
        Assert.Throws<ArgumentNullException>(() => WindowPlacement.Fit(new SavedWindow(0, 0, 10, 10, false), null!, MinWidth, MinHeight));
        Assert.Throws<ArgumentNullException>(() => WindowPlacement.Fit(null!, new[] { Primary }, MinWidth, MinHeight));
        Assert.Throws<ArgumentNullException>(() => WindowPlacement.Fit(new SavedWindow(0, 0, 10, 10, false), new ScreenArea[] { null! }, MinWidth, MinHeight));
    }
}
