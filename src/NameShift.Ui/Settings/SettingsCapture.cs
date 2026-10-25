using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Settings;

/// <summary>The window's show state at close, without WinForms.</summary>
public enum WindowShowState
{
    /// <summary>Neither maximised nor minimised.</summary>
    Normal,

    /// <summary>Maximised.</summary>
    Maximized,

    /// <summary>Minimised.</summary>
    Minimized,
}

/// <summary>A window rectangle in device (screen) pixels, as the window reports it.</summary>
public readonly struct DeviceRect
{
    public DeviceRect(int left, int top, int width, int height)
    {
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    public int Left { get; }

    public int Top { get; }

    public int Width { get; }

    public int Height { get; }
}

/// <summary>What the window knows at close: plain values only.</summary>
public sealed class SettingsFacts
{
    /// <param name="state">The window's show state.</param>
    /// <param name="bounds">The window's bounds now (device pixels).</param>
    /// <param name="restoreBounds">The bounds the window returns to from maximised or minimised (device pixels).</param>
    /// <param name="maximizedBeforeMinimize">Whether the window was maximised before it was minimised.</param>
    /// <param name="scale">The screen's scale: DPI divided by 96.</param>
    /// <param name="splitterDistance">The folder tree's splitter in device pixels.</param>
    /// <param name="currentFolder">The folder shown now, or null when none is.</param>
    /// <param name="startFolder">The last folder read at start, or null.</param>
    /// <param name="collapsedPanels">Each rule panel's collapse state (NS-84), or null for none.</param>
    public SettingsFacts(
        WindowShowState state,
        DeviceRect bounds,
        DeviceRect restoreBounds,
        bool maximizedBeforeMinimize,
        double scale,
        int splitterDistance,
        string? currentFolder,
        string? startFolder,
        IReadOnlyDictionary<RuleStage, bool>? collapsedPanels)
    {
        State = state;
        Bounds = bounds;
        RestoreBounds = restoreBounds;
        MaximizedBeforeMinimize = maximizedBeforeMinimize;
        Scale = scale;
        SplitterDistance = splitterDistance;
        CurrentFolder = currentFolder;
        StartFolder = startFolder;
        CollapsedPanels = collapsedPanels;
    }

    public WindowShowState State { get; }

    public DeviceRect Bounds { get; }

    public DeviceRect RestoreBounds { get; }

    public bool MaximizedBeforeMinimize { get; }

    public double Scale { get; }

    public int SplitterDistance { get; }

    public string? CurrentFolder { get; }

    public string? StartFolder { get; }

    public IReadOnlyDictionary<RuleStage, bool>? CollapsedPanels { get; }
}

/// <summary>Turns the window's facts at close into the settings to save (NS-37, NS-84).</summary>
public static class SettingsCapture
{
    /// <summary>The settings to save for these facts: the window placement (null when the window never had a size),
    /// the splitter in 96-DPI logical pixels (null when negative), the folder shown now or else the one read at
    /// start, and the collapsed panels as they are.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="facts"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The scale is not above 0.</exception>
    public static AppSettings Capture(SettingsFacts facts)
    {
        if (facts == null)
        {
            throw new ArgumentNullException(nameof(facts));
        }

        if (!(facts.Scale > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(facts), "The scale must be above 0.");
        }

        DeviceRect b = facts.State == WindowShowState.Normal ? facts.Bounds : facts.RestoreBounds;
        bool maximized = facts.State == WindowShowState.Maximized
            || (facts.State == WindowShowState.Minimized && facts.MaximizedBeforeMinimize);
        SavedWindow? window = b.Width > 0 && b.Height > 0
            ? WindowPlacement.ToSaved(b.Left, b.Top, b.Width, b.Height, facts.Scale, maximized)
            : null;
        int split = (int)Math.Round(facts.SplitterDistance / facts.Scale, MidpointRounding.AwayFromZero);

        // A start that never showed a folder keeps the one read at start instead of erasing it.
        return new AppSettings(
            window, split >= 0 ? split : null, facts.CurrentFolder ?? facts.StartFolder, facts.CollapsedPanels);
    }
}
