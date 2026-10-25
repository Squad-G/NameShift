using System;
using System.Collections.Generic;
using NameShift.Core;
using NameShift.Ui.Settings;
using Xunit;

namespace NameShift.Ui.Tests.Settings;

/// <summary>NS-92: the computation that <c>MainForm.SaveSettings</c> used to hold.</summary>
public class SettingsCaptureTests
{
    private static readonly DeviceRect DefaultBounds = new DeviceRect(100, 50, 1200, 800);
    private static readonly DeviceRect DefaultRestore = new DeviceRect(10, 20, 1000, 700);

    private static SettingsFacts Facts(
        WindowShowState state = WindowShowState.Normal,
        DeviceRect? bounds = null,
        DeviceRect? restore = null,
        bool wasMaximized = false,
        double scale = 1.0,
        int split = 260,
        string? current = null,
        string? start = null,
        IReadOnlyDictionary<RuleStage, bool>? panels = null)
    {
        return new SettingsFacts(
            state, bounds ?? DefaultBounds, restore ?? DefaultRestore, wasMaximized, scale, split, current, start, panels);
    }

    private static void AssertSettings(AppSettings expected, AppSettings actual)
    {
        if (expected.Window == null)
        {
            Assert.Null(actual.Window);
        }
        else
        {
            Assert.NotNull(actual.Window);
            Assert.Equal(expected.Window!.Left, actual.Window!.Left);
            Assert.Equal(expected.Window.Top, actual.Window.Top);
            Assert.Equal(expected.Window.Width, actual.Window.Width);
            Assert.Equal(expected.Window.Height, actual.Window.Height);
            Assert.Equal(expected.Window.Maximized, actual.Window.Maximized);
        }

        Assert.Equal(expected.MainSplit, actual.MainSplit);
        Assert.Equal(expected.LastFolder, actual.LastFolder);
        Assert.Equal(expected.CollapsedPanels.Count, actual.CollapsedPanels.Count);
        foreach (KeyValuePair<RuleStage, bool> pair in expected.CollapsedPanels)
        {
            Assert.True(actual.CollapsedPanels.TryGetValue(pair.Key, out bool value));
            Assert.Equal(pair.Value, value);
        }
    }

    private static void AssertWindow(SavedWindow? actual, int left, int top, int width, int height, bool maximized)
    {
        Assert.NotNull(actual);
        Assert.Equal(left, actual!.Left);
        Assert.Equal(top, actual.Top);
        Assert.Equal(width, actual.Width);
        Assert.Equal(height, actual.Height);
        Assert.Equal(maximized, actual.Maximized);
    }

    [Fact]
    public void Normal_UsesBounds()
    {
        AppSettings s = SettingsCapture.Capture(Facts(scale: 1.5));
        SavedWindow expected = WindowPlacement.ToSaved(100, 50, 1200, 800, 1.5, false);
        AssertWindow(s.Window, expected.Left, expected.Top, expected.Width, expected.Height, expected.Maximized);
        AssertWindow(s.Window, 100, 50, 800, 533, false);
    }

    [Fact]
    public void Maximized_UsesRestoreBounds()
    {
        AppSettings s = SettingsCapture.Capture(Facts(WindowShowState.Maximized));
        AssertWindow(s.Window, 10, 20, 1000, 700, true);
    }

    [Fact]
    public void Minimized_FromMaximized_IsMaximized()
    {
        AppSettings s = SettingsCapture.Capture(Facts(WindowShowState.Minimized, wasMaximized: true));
        AssertWindow(s.Window, 10, 20, 1000, 700, true);
    }

    [Fact]
    public void Minimized_FromNormal_NotMaximized()
    {
        AppSettings s = SettingsCapture.Capture(Facts(WindowShowState.Minimized, wasMaximized: false));
        AssertWindow(s.Window, 10, 20, 1000, 700, false);
    }

    [Fact]
    public void Normal_IgnoresMaximizedBeforeMinimize()
    {
        AppSettings s = SettingsCapture.Capture(Facts(wasMaximized: true));
        AssertWindow(s.Window, 100, 50, 1200, 800, false);
    }

    [Theory]
    [InlineData(0, 800)]
    [InlineData(1200, 0)]
    [InlineData(-5, 800)]
    [InlineData(1200, -1)]
    public void NoSize_NoWindow(int width, int height)
    {
        var bad = new DeviceRect(5, 6, width, height);
        AppSettings normal = SettingsCapture.Capture(Facts(bounds: bad, current: @"D:\Now", split: 300, scale: 1.5));
        Assert.Null(normal.Window);
        Assert.Equal(200, normal.MainSplit);
        Assert.Equal(@"D:\Now", normal.LastFolder);

        AppSettings maximized = SettingsCapture.Capture(
            Facts(WindowShowState.Maximized, restore: bad, current: @"D:\Now", split: 300, scale: 1.5));
        Assert.Null(maximized.Window);
        Assert.Equal(200, maximized.MainSplit);
        Assert.Equal(@"D:\Now", maximized.LastFolder);
    }

    [Theory]
    [InlineData(301, 1.25, 241)]
    [InlineData(150, 1.5, 100)]
    [InlineData(101, 2.0, 51)]
    [InlineData(260, 1.0, 260)]
    [InlineData(0, 1.5, 0)]
    [InlineData(1, 2.0, 1)]
    public void Split_Rounding(int distance, double scale, int expected)
    {
        Assert.Equal(expected, SettingsCapture.Capture(Facts(scale: scale, split: distance)).MainSplit);
    }

    [Theory]
    [InlineData(-1, 1.0)]
    [InlineData(-3, 2.0)]
    [InlineData(-1, 2.0)]
    public void Split_Negative_Null(int distance, double scale)
    {
        Assert.Null(SettingsCapture.Capture(Facts(scale: scale, split: distance)).MainSplit);
    }

    [Fact]
    public void Folder_CurrentWins()
    {
        Assert.Equal(@"D:\Now", SettingsCapture.Capture(Facts(current: @"D:\Now", start: @"D:\Start")).LastFolder);
    }

    [Fact]
    public void Folder_NoneShown_KeepsStart()
    {
        Assert.Equal(@"D:\Start", SettingsCapture.Capture(Facts(current: null, start: @"D:\Start")).LastFolder);
    }

    [Fact]
    public void Folder_Neither_Null()
    {
        Assert.Null(SettingsCapture.Capture(Facts()).LastFolder);
    }

    [Fact]
    public void Folder_BlankCurrent_DoesNotFallBack()
    {
        // Pins the old "??": a blank but non-null current folder does not fall back, and AppSettings makes it null.
        Assert.Null(SettingsCapture.Capture(Facts(current: "", start: @"D:\Start")).LastFolder);
    }

    [Fact]
    public void CollapsedPanels_InIsOut()
    {
        var map = new Dictionary<RuleStage, bool>();
        RuleStage[] stages = (RuleStage[])Enum.GetValues(typeof(RuleStage));
        map[stages[0]] = true;
        map[stages[1]] = false;
        AppSettings s = SettingsCapture.Capture(Facts(panels: map));
        Assert.Equal(2, s.CollapsedPanels.Count);
        Assert.True(s.CollapsedPanels[stages[0]]);
        Assert.False(s.CollapsedPanels[stages[1]]);

        Assert.Empty(SettingsCapture.Capture(Facts(panels: null)).CollapsedPanels);
    }

    [Fact]
    public void NullFacts_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => SettingsCapture.Capture(null!));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    public void ZeroScale_Throws(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SettingsCapture.Capture(Facts(scale: scale)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => SettingsCapture.Capture(Facts(bounds: new DeviceRect(0, 0, 0, 0), scale: scale)));
    }

    /// <summary>The computation of the old <c>MainForm.SaveSettings</c>, verbatim, with plain values.</summary>
    private static AppSettings Old(
        WindowShowState windowState,
        DeviceRect bounds,
        DeviceRect restoreBounds,
        bool maximizedBeforeMinimize,
        double scale,
        int splitterDistance,
        string? currentFolder,
        string? savedLastFolder,
        IReadOnlyDictionary<RuleStage, bool> states)
    {
        DeviceRect b = windowState == WindowShowState.Normal ? bounds : restoreBounds;
        bool maximized = windowState == WindowShowState.Maximized
            || (windowState == WindowShowState.Minimized && maximizedBeforeMinimize);
        SavedWindow? window = b.Width > 0 && b.Height > 0
            ? WindowPlacement.ToSaved(b.Left, b.Top, b.Width, b.Height, scale, maximized)
            : null;
        int split = (int)Math.Round(splitterDistance / scale, MidpointRounding.AwayFromZero);

        return new AppSettings(
            window, split >= 0 ? split : null, currentFolder ?? savedLastFolder, states);
    }

    [Fact]
    public void MatchesTheOldCode()
    {
        double[] scales = { 1, 1.25, 1.5, 1.75, 2, 2.5 };
        var splits = new List<int>();
        for (int d = -2; d <= 620; d += 7)
        {
            splits.Add(d);
        }

        splits.AddRange(new[] { 101, 150, 301 });
        DeviceRect[] rects = { DefaultBounds, new DeviceRect(0, 0, 0, 0) };
        (string? Current, string? Start)[] folders =
        {
            (@"D:\Now", @"D:\Start"), (null, @"D:\Start"), (null, null), ("", @"D:\Start"),
        };
        RuleStage[] stages = (RuleStage[])Enum.GetValues(typeof(RuleStage));
        var panels = new Dictionary<RuleStage, bool> { [stages[0]] = true, [stages[1]] = false };

        int cases = 0;
        foreach (WindowShowState state in (WindowShowState[])Enum.GetValues(typeof(WindowShowState)))
        {
            foreach (bool was in new[] { true, false })
            {
                foreach (double scale in scales)
                {
                    foreach (int split in splits)
                    {
                        foreach (DeviceRect rect in rects)
                        {
                            foreach ((string? current, string? start) in folders)
                            {
                                // The empty rectangle goes once through the restore bounds, once through the bounds.
                                var facts = new SettingsFacts(
                                    state, DefaultBounds, rect, was, scale, split, current, start, panels);
                                AssertSettings(
                                    Old(state, DefaultBounds, rect, was, scale, split, current, start, panels),
                                    SettingsCapture.Capture(facts));
                                var facts2 = new SettingsFacts(
                                    state, rect, DefaultRestore, was, scale, split, current, start, panels);
                                AssertSettings(
                                    Old(state, rect, DefaultRestore, was, scale, split, current, start, panels),
                                    SettingsCapture.Capture(facts2));
                                cases += 2;
                            }
                        }
                    }
                }
            }
        }

        Assert.True(cases > 10000);
    }
}
