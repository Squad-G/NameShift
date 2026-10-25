using System;
using System.Collections.Generic;
using System.Linq;
using NameShift.Ui.Rules;
using Xunit;

namespace NameShift.Ui.Tests.Rules;

/// <summary>Margin 4 throughout: a panel takes its width + 8 across and its height + 8 down.</summary>
public sealed class PanelFlowTests
{
    private const int Margin = 4;
    private const int NoMinimum = 0;
    private const int NoCap = 10000;

    private static PanelBox Collapsed(int width = 240) => new PanelBox(width, 300, 30, collapsed: true);

    private static PanelBox Open(int openHeight, int width = 240) => new PanelBox(width, openHeight, 30, collapsed: false);

    private static int[][] Rows(PanelFlowResult result) => result.Rows.Select(r => r.ToArray()).ToArray();

    [Fact]
    public void Zero_GivesMinimum()
    {
        PanelFlowResult r = PanelFlow.Measure(new PanelBox[0], Margin, 1000, 90, NoCap);

        Assert.Empty(r.Rows);
        Assert.Equal(0, r.ContentHeight);
        Assert.Equal(90, r.Height);
        Assert.False(r.NeedsScroll);
    }

    [Fact]
    public void AllCollapsed_FewShortRows()
    {
        // 11 panels of 248 across in 1000: 4 per row (992), 3 rows of 38.
        var panels = Enumerable.Range(0, 11).Select(_ => Collapsed()).ToList();

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 1000, NoMinimum, NoCap);

        Assert.Equal(new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 8, 9, 10 } }, Rows(r));
        Assert.Equal(114, r.ContentHeight);
        Assert.Equal(114, r.Height);
        Assert.False(r.NeedsScroll);
    }

    [Fact]
    public void OneOpen_ItsRowIsTall()
    {
        // Rows: [0 1 2 3] with panel 1 open (308), [4 5] collapsed (38).
        var panels = new List<PanelBox> { Collapsed(), Open(300), Collapsed(), Collapsed(), Collapsed(), Collapsed() };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 1000, NoMinimum, NoCap);

        Assert.Equal(new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 5 } }, Rows(r));
        Assert.Equal(308 + 38, r.ContentHeight);
    }

    [Fact]
    public void TwoOpenOneRow_TallestWins()
    {
        var panels = new List<PanelBox> { Open(100), Open(200), Collapsed() };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 1000, NoMinimum, NoCap);

        Assert.Single(r.Rows);
        Assert.Equal(208, r.ContentHeight);
    }

    [Fact]
    public void TwoOpenSeparateRows_Add()
    {
        // Width 500 holds 2 panels (496): rows [0 1] (108) and [2 3] (208).
        var panels = new List<PanelBox> { Open(100), Collapsed(), Open(200), Collapsed() };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 500, NoMinimum, NoCap);

        Assert.Equal(new[] { new[] { 0, 1 }, new[] { 2, 3 } }, Rows(r));
        Assert.Equal(108 + 208, r.ContentHeight);
    }

    [Fact]
    public void CollapsingTallest_ShrinksRowToNext()
    {
        var before = new List<PanelBox> { Open(100), Open(200), Open(150) };
        var after = new List<PanelBox> { Open(100), new PanelBox(240, 200, 30, collapsed: true), Open(150) };

        Assert.Equal(208, PanelFlow.Measure(before, Margin, 1000, NoMinimum, NoCap).ContentHeight);
        Assert.Equal(158, PanelFlow.Measure(after, Margin, 1000, NoMinimum, NoCap).ContentHeight);
    }

    [Fact]
    public void NarrowerWidth_MoreRows_Taller()
    {
        var panels = new List<PanelBox> { Open(100), Open(100), Open(100), Open(100) };

        PanelFlowResult wide = PanelFlow.Measure(panels, Margin, 1000, NoMinimum, NoCap);
        PanelFlowResult narrow = PanelFlow.Measure(panels, Margin, 500, NoMinimum, NoCap);

        Assert.Single(wide.Rows);
        Assert.Equal(108, wide.ContentHeight);
        Assert.Equal(2, narrow.Rows.Count);
        Assert.Equal(216, narrow.ContentHeight);
    }

    [Fact]
    public void WiderThanWidth_AloneOnItsRow()
    {
        // Width 300 holds one 248 panel; the 400 wide one sits alone, and the next starts a new row.
        var panels = new List<PanelBox> { Collapsed(), Collapsed(400), Collapsed() };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 300, NoMinimum, NoCap);

        Assert.Equal(new[] { new[] { 0 }, new[] { 1 }, new[] { 2 } }, Rows(r));
        Assert.Equal(3 * 38, r.ContentHeight);
    }

    [Fact]
    public void WiderThanWidth_First_AloneOnItsRow()
    {
        var panels = new List<PanelBox> { Collapsed(400), Collapsed(), Collapsed() };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 600, NoMinimum, NoCap);

        Assert.Equal(new[] { new[] { 0 }, new[] { 1, 2 } }, Rows(r));
    }

    [Fact]
    public void AboveCap_CapAndScroll()
    {
        var panels = new List<PanelBox> { Open(300), Open(300) };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 300, NoMinimum, 500);

        Assert.Equal(616, r.ContentHeight);
        Assert.Equal(500, r.Height);
        Assert.True(r.NeedsScroll);
    }

    [Fact]
    public void AtCap_NoScroll()
    {
        var panels = new List<PanelBox> { Open(300) };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 300, NoMinimum, 308);

        Assert.Equal(308, r.Height);
        Assert.False(r.NeedsScroll);
    }

    [Fact]
    public void BelowMinimum_Minimum()
    {
        var panels = new List<PanelBox> { Collapsed() };

        PanelFlowResult r = PanelFlow.Measure(panels, Margin, 1000, 90, NoCap);

        Assert.Equal(38, r.ContentHeight);
        Assert.Equal(90, r.Height);
        Assert.False(r.NeedsScroll);
    }

    [Fact]
    public void MinimumEqualsCap_Allowed()
    {
        PanelFlowResult r = PanelFlow.Measure(new[] { Open(300) }, Margin, 1000, 90, 90);

        Assert.Equal(90, r.Height);
        Assert.True(r.NeedsScroll);
    }

    [Fact]
    public void Boundary_ExactWidthFits()
    {
        // Three panels of 240 with margin 4 need exactly 3 * 248 = 744.
        var panels = new List<PanelBox> { Collapsed(), Collapsed(), Collapsed() };

        Assert.Single(PanelFlow.Measure(panels, Margin, 744, NoMinimum, NoCap).Rows);
        PanelFlowResult less = PanelFlow.Measure(panels, Margin, 743, NoMinimum, NoCap);
        Assert.Equal(new[] { new[] { 0, 1 }, new[] { 2 } }, Rows(less));
        Assert.Equal(76, less.ContentHeight);
    }

    [Fact]
    public void Settle_FirstPassFits_NoSecondPass()
    {
        // At 744 the three fit in one row of 308, under the cap: the width beside a scroll bar is never tried.
        var panels = new List<PanelBox> { Open(300), Collapsed(), Collapsed() };

        PanelFlowResult r = PanelFlow.Settle(panels, Margin, 744, 17, NoMinimum, 400);

        Assert.Single(r.Rows);
        Assert.Equal(308, r.Height);
        Assert.False(r.NeedsScroll);
        Assert.Equal(744, r.UsableWidth);
    }

    [Fact]
    public void Settle_FirstPassScrolls_SecondPassUsesNarrowerWidth()
    {
        // Full width 1000: rows [0 1 2 3] (308) and [4] (308) = 616 > cap 400, so scroll.
        // Beside the bar, 1000 - 17 = 983 holds 3 (744): rows [0 1 2] [3 4] = 616, still scrolls.
        var panels = new List<PanelBox> { Open(300), Collapsed(), Collapsed(), Collapsed(), Open(300) };

        PanelFlowResult first = PanelFlow.Measure(panels, Margin, 1000, NoMinimum, 400);
        PanelFlowResult r = PanelFlow.Settle(panels, Margin, 1000, 17, NoMinimum, 400);

        Assert.Equal(2, first.Rows.Count);
        Assert.True(first.NeedsScroll);
        Assert.Equal(new[] { new[] { 0, 1, 2 }, new[] { 3, 4 } }, Rows(r));
        Assert.Equal(616, r.ContentHeight);
        Assert.Equal(400, r.Height);
        Assert.True(r.NeedsScroll);
    }

    [Fact]
    public void Settle_FirstPassScrolls_SecondPassWrapsOneMore()
    {
        // Full width 992 holds 4 per row: [0..3] [4..7] = 308 + 38 = 346 > cap 300. Beside the bar (975) only 3 fit:
        // [0 1 2] [3 4 5] [6 7] = 308 + 38 + 38 = 384.
        var panels = new List<PanelBox> { Open(300) };
        panels.AddRange(Enumerable.Range(0, 7).Select(_ => Collapsed()));

        PanelFlowResult r = PanelFlow.Settle(panels, Margin, 992, 17, NoMinimum, 300);

        Assert.Equal(3, r.Rows.Count);
        Assert.Equal(384, r.ContentHeight);
        Assert.Equal(300, r.Height);
        Assert.True(r.NeedsScroll);
        Assert.Equal(975, r.UsableWidth);
    }

    [Fact]
    public void Settle_SecondPassRegroupsUnderCap_KeepsNarrowWidth()
    {
        // Eight collapsed panels, then three open. At 1240 (5 per row): [0-4] (38), [5-9] (308), [10] (308) = 654,
        // above the cap. Beside the bar (1223, 4 per row): [0-3] (38), [4-7] (38), [8-10] (308) = 384, under the cap:
        // no scroll, but the rows stay wrapped at 1223 (wrapping back at 1240 would be 654 again).
        var panels = Enumerable.Range(0, 8).Select(_ => Collapsed()).ToList();
        panels.AddRange(new[] { Open(300), Open(300), Open(300) });

        PanelFlowResult r = PanelFlow.Settle(panels, Margin, 1240, 17, NoMinimum, 500);

        Assert.Equal(new[] { new[] { 0, 1, 2, 3 }, new[] { 4, 5, 6, 7 }, new[] { 8, 9, 10 } }, Rows(r));
        Assert.Equal(384, r.ContentHeight);
        Assert.Equal(384, r.Height);
        Assert.False(r.NeedsScroll);
        Assert.Equal(1223, r.UsableWidth);
    }

    [Fact]
    public void Measure_UsableWidth_IsTheWidthGiven()
    {
        Assert.Equal(777, PanelFlow.Measure(new[] { Collapsed() }, Margin, 777, NoMinimum, NoCap).UsableWidth);
    }

    [Fact]
    public void Settle_BoundaryWithScrollBar()
    {
        // 744 holds three panels exactly, but 744 - 17 does not: with a scroll the third wraps.
        var panels = new List<PanelBox> { Open(300), Open(300), Open(300) };

        PanelFlowResult fits = PanelFlow.Settle(panels, Margin, 744, 17, NoMinimum, 400);
        PanelFlowResult scrolls = PanelFlow.Settle(panels, Margin, 744, 17, NoMinimum, 300);

        Assert.Single(fits.Rows);
        Assert.False(fits.NeedsScroll);
        Assert.Equal(new[] { new[] { 0, 1 }, new[] { 2 } }, Rows(scrolls));
        Assert.Equal(616, scrolls.ContentHeight);
        Assert.True(scrolls.NeedsScroll);
        Assert.Equal(744, fits.UsableWidth);
        Assert.Equal(727, scrolls.UsableWidth);
    }

    [Fact]
    public void Collapsed_UsesCollapsedHeight()
    {
        Assert.Equal(30, new PanelBox(240, 300, 30, collapsed: true).Height);
        Assert.Equal(300, new PanelBox(240, 300, 30, collapsed: false).Height);
    }

    [Fact]
    public void Invalid_Throws()
    {
        var panels = new List<PanelBox> { Collapsed() };

        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFlow.Measure(panels, -1, 1000, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFlow.Measure(panels, Margin, -1, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFlow.Measure(panels, Margin, 1000, -1, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFlow.Measure(panels, Margin, 1000, 101, 100));
        Assert.Throws<ArgumentNullException>(() => PanelFlow.Measure(null!, Margin, 1000, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFlow.Settle(panels, Margin, -1, 17, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFlow.Settle(panels, Margin, 1000, -1, 0, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => PanelFlow.Settle(panels, Margin, 1000, 17, 101, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PanelBox(-1, 0, 0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PanelBox(0, -1, 0, false));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PanelBox(0, 0, -1, false));
    }
}
