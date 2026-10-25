using System;

namespace NameShift.Ui.Rules;

/// <summary>The size of one rule panel for <see cref="PanelFlow"/>, in device pixels, without its margin.</summary>
public readonly struct PanelBox
{
    /// <exception cref="ArgumentOutOfRangeException">A size is negative.</exception>
    public PanelBox(int width, int openHeight, int collapsedHeight, bool collapsed)
    {
        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (openHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(openHeight));
        }

        if (collapsedHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(collapsedHeight));
        }

        Width = width;
        OpenHeight = openHeight;
        CollapsedHeight = collapsedHeight;
        Collapsed = collapsed;
    }

    /// <summary>The panel's width, the same open or collapsed.</summary>
    public int Width { get; }

    /// <summary>The height with the content shown.</summary>
    public int OpenHeight { get; }

    /// <summary>The height of the title line alone.</summary>
    public int CollapsedHeight { get; }

    /// <summary>True when only the title line shows.</summary>
    public bool Collapsed { get; }

    /// <summary>The height the panel takes now.</summary>
    public int Height => Collapsed ? CollapsedHeight : OpenHeight;
}
