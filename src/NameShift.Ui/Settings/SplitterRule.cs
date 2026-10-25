using System;

namespace NameShift.Ui.Settings;

/// <summary>The saved folder-tree splitter, applied at start (NS-37 AC 6).</summary>
public static class SplitterRule
{
    /// <summary>The splitter distance to apply: the saved value when it is 0 or more and fits the container, else the
    /// fallback; then kept between the panels' limits (the minimum wins when the container is too small for both).
    /// Every argument is in the same unit.</summary>
    public static int Clamp(int? saved, int fallback, int container, int min, int max)
    {
        int value = saved is int v && v >= 0 && v <= container ? v : fallback;
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }
}
