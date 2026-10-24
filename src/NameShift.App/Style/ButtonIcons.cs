using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace NameShift.App.Style;

/// <summary>The three small icons of the action buttons, drawn in code with simple strokes (no image files).</summary>
internal enum ButtonIcon
{
    /// <summary>A pencil.</summary>
    Rename,

    /// <summary>An arrow curving back to the left.</summary>
    Revert,

    /// <summary>A small x inside a rounded square.</summary>
    Reset,
}

internal static class ButtonIcons
{
    /// <summary>Draws one icon as a new 32-bit ARGB bitmap of size x size device pixels; the caller owns and disposes it.</summary>
    public static Bitmap Create(ButtonIcon icon, int size, Color color)
    {
        size = Math.Max(size, 8);
        var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            using (var pen = new Pen(color, Math.Max(1.5f, size / 10f)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (icon)
                {
                    case ButtonIcon.Rename:
                        DrawPencil(g, pen, size);
                        break;
                    case ButtonIcon.Revert:
                        DrawBackArrow(g, pen, size);
                        break;
                    default:
                        DrawReset(g, pen, size);
                        break;
                }
            }

            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    // A slanted body with a pointed tip at the bottom left and a line across the shoulder.
    private static void DrawPencil(Graphics g, Pen pen, int size)
    {
        float s = size - 2f; // 1 px inner margin on every side
        PointF Point(float along, float across)
        {
            // Tip at (0.1, 0.9); the body runs up and to the right; "across" is the perpendicular.
            const float k = 0.70710678f;
            return new PointF(1f + (0.1f + k * along + k * across) * s, 1f + (0.9f - k * along + k * across) * s);
        }

        const float half = 0.12f;
        PointF[] outline =
        {
            Point(0f, 0f),
            Point(0.3f, half),
            Point(0.95f, half),
            Point(0.95f, -half),
            Point(0.3f, -half),
        };
        g.DrawPolygon(pen, outline);
        g.DrawLine(pen, Point(0.3f, half), Point(0.3f, -half));
    }

    // An undo arc: starts at the upper left, turns clockwise down the right side and ends at the lower left; the head points down and left.
    private static void DrawBackArrow(Graphics g, Pen pen, int size)
    {
        float s = size - 2f;
        var box = new RectangleF(1f + 0.12f * s, 1f + 0.3f * s, 0.78f * s, 0.62f * s);
        const float start = 215f;
        g.DrawArc(pen, box, start, 250f);

        double rad = start * Math.PI / 180.0;
        float tipX = box.X + box.Width / 2f + (box.Width / 2f) * (float)Math.Cos(rad);
        float tipY = box.Y + box.Height / 2f + (box.Height / 2f) * (float)Math.Sin(rad);
        // Travel direction at the tip (clockwise); the wings of the head go back along it, 40 degrees to each side.
        double tx = -(box.Width / 2f) * Math.Sin(rad);
        double ty = (box.Height / 2f) * Math.Cos(rad);
        double angle = Math.Atan2(ty, tx);
        float wing = 0.32f * s;
        foreach (double turn in new[] { 40.0 * Math.PI / 180.0, -40.0 * Math.PI / 180.0 })
        {
            double a = angle + turn;
            g.DrawLine(pen, tipX, tipY, tipX + wing * (float)Math.Cos(a), tipY + wing * (float)Math.Sin(a));
        }
    }

    // A rounded square with an x in it.
    private static void DrawReset(Graphics g, Pen pen, int size)
    {
        float s = size - 2f;
        float edge = 0.9f * s;
        float offset = 1f + (s - edge) / 2f;
        float radius = 0.2f * s;
        float diameter = radius * 2f;
        using (var path = new GraphicsPath())
        {
            path.AddArc(offset, offset, diameter, diameter, 180f, 90f);
            path.AddArc(offset + edge - diameter, offset, diameter, diameter, 270f, 90f);
            path.AddArc(offset + edge - diameter, offset + edge - diameter, diameter, diameter, 0f, 90f);
            path.AddArc(offset, offset + edge - diameter, diameter, diameter, 90f, 90f);
            path.CloseFigure();
            g.DrawPath(pen, path);
        }

        float lo = offset + 0.3f * edge;
        float hi = offset + 0.7f * edge;
        g.DrawLine(pen, lo, lo, hi, hi);
        g.DrawLine(pen, lo, hi, hi, lo);
    }
}
