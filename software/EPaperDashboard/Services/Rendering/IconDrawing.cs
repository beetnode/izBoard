using System.Numerics;
using EPaperDashboard.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Color = SixLabors.ImageSharp.Color;
using PointF = SixLabors.ImageSharp.PointF;
using RectangleF = SixLabors.ImageSharp.RectangleF;

namespace EPaperDashboard.Services.Rendering;

public static class IconDrawing
{
    internal enum AppIconShade { Darkest, Darker, Base, Light, Lighter, Lightest }

    /// <summary>One tile of the app icon: its corners in pixels and whether it is a rounded rectangle.</summary>
    internal sealed record AppIconTile(PointF[] Points, bool Rounded, float CornerRadius, AppIconShade Shade);

    public static void DrawAppIcon(Image<Rgba32> image, Color accentColor, RectangleF bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var p = accentColor.ToPixel<Rgba32>();
        Color Darken(float f) => new(new Rgba32((byte)(p.R * f), (byte)(p.G * f), (byte)(p.B * f), p.A));
        Color Lighten(float f) => new(new Rgba32((byte)(p.R + (255 - p.R) * f), (byte)(p.G + (255 - p.G) * f), (byte)(p.B + (255 - p.B) * f), p.A));
        Color ShadeColor(AppIconShade shade) => shade switch
        {
            AppIconShade.Darkest => Darken(0.3f),
            AppIconShade.Darker => Darken(0.7f),
            AppIconShade.Light => Lighten(0.2f),
            AppIconShade.Lighter => Lighten(0.4f),
            AppIconShade.Lightest => Lighten(0.6f),
            _ => accentColor,
        };

        foreach (var tile in AppIconLayout(bounds))
        {
            var pts = tile.Points;
            IPath path = tile.Rounded
                ? BuildRoundedRect(pts[0].X, pts[0].Y, pts[2].X - pts[0].X, pts[2].Y - pts[0].Y, tile.CornerRadius)
                : new Polygon(new LinearLineSegment(pts));
            image.Mutate(ctx => ctx.Fill(ShadeColor(tile.Shade), path));
        }
    }

    /// <summary>
    /// Lays out the app icon tiles for <paramref name="bounds"/>. Geometry follows icon-tab.svg (370-unit
    /// viewBox, tile block x 20..350, y 20..324, 12-unit gaps), with the block centred vertically.
    /// Tile edges sit on whole pixels and every gap, including the tilted one, is the same pixel width.
    /// </summary>
    internal static IReadOnlyList<AppIconTile> AppIconLayout(RectangleF bounds)
    {
        const float vb = 370f;
        const float yShift = 13f;
        var scale = Math.Min(bounds.Width / vb, bounds.Height / vb);
        var ox = bounds.X + (bounds.Width - vb * scale) / 2f;
        var oy = bounds.Y + (bounds.Height - vb * scale) / 2f + yShift * scale;
        var gap = Math.Max(1f, MathF.Round(12f * scale));
        var cr = 4f * scale;

        // Each tile starts on a snapped grid line and ends exactly one gap before the next one starts.
        float X(float v) => MathF.Round(ox + v * scale);
        float Y(float v) => MathF.Round(oy + v * scale);
        float left = X(20), col2 = X(122), topSplit = X(268), bottomSplit = X(218), right = X(350);
        float top = Y(20), row2 = Y(128), row3 = Y(236), bottom = Y(324);

        AppIconTile Rect(float x1, float y1, float x2, float y2, AppIconShade shade) =>
            new([new(x1, y1), new(x2, y1), new(x2, y2), new(x1, y2)], true, cr, shade);

        // The middle row is split by a tilted gap parallel to the right tile's slanted edge. Shifting the
        // left tile by gap * length / height keeps the perpendicular width equal to the straight gaps.
        float midBottom = row3 - gap;
        var dx = topSplit - bottomSplit;
        var dy = midBottom - row2;
        var shift = gap * MathF.Sqrt(dx * dx + dy * dy) / dy;

        return
        [
            Rect(left, top, col2 - gap, row2 - gap, AppIconShade.Darkest),
            Rect(left, row2, col2 - gap, bottom, AppIconShade.Darker),
            Rect(col2, top, topSplit - gap, row2 - gap, AppIconShade.Base),
            Rect(topSplit, top, right, row2 - gap, AppIconShade.Light),
            new([new(col2, row2), new(topSplit - shift, row2), new(bottomSplit - shift, midBottom), new(col2, midBottom)],
                false, 0, AppIconShade.Lightest),
            new([new(topSplit, row2), new(right, row2), new(right, midBottom), new(bottomSplit, midBottom)],
                false, 0, AppIconShade.Lighter),
            Rect(col2, row3, bottomSplit - gap, bottom, AppIconShade.Light),
            Rect(bottomSplit, row3, right, bottom, AppIconShade.Lighter),
        ];
    }

    public static IPath BuildRoundedRect(float x, float y, float w, float h, float cr)
    {
        if (cr < 0.5f)
            return new RectangularPolygon(x, y, w, h);

        cr = Math.Min(cr, Math.Min(w, h) / 2f);
        var pb = new PathBuilder();
        pb.MoveTo(new PointF(x + cr, y));
        pb.LineTo(new PointF(x + w - cr, y));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w, y + cr));
        pb.LineTo(new PointF(x + w, y + h - cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + w - cr, y + h));
        pb.LineTo(new PointF(x + cr, y + h));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x, y + h - cr));
        pb.LineTo(new PointF(x, y + cr));
        pb.ArcTo(cr, cr, 0, false, true, new PointF(x + cr, y));
        pb.CloseFigure();
        return pb.Build();
    }
}
