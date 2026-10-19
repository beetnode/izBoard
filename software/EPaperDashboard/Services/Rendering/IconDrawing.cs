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
    public static void DrawAppIcon(Image<Rgba32> image, Color accentColor, RectangleF bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        // Geometry matches icon-tab.svg: 370-unit viewBox, tile block spans x 20..350, y 20..324,
        // separated by 12-unit gaps. The block is centred vertically (the SVG uses viewBox min-y -13).
        const float vb = 370f;
        const float yShift = 13f;
        const float gap = 12f;
        var scale = Math.Min(bounds.Width / vb, bounds.Height / vb);
        var ox = bounds.X + (bounds.Width - vb * scale) / 2f;
        var oy = bounds.Y + (bounds.Height - vb * scale) / 2f + yShift * scale;

        // Tile edges are snapped to whole pixels so gaps stay crisp, and an edge that follows a
        // gap is pushed at least one pixel past the previous tile so small icons keep their spacing.
        float SnapX(float v) => MathF.Round(v * scale + ox);
        float SnapY(float v) => MathF.Round(v * scale + oy);
        float LeadX(float v) => Math.Max(SnapX(v), SnapX(v - gap) + 1);
        float LeadY(float v) => Math.Max(SnapY(v), SnapY(v - gap) + 1);

        var p = accentColor.ToPixel<Rgba32>();
        var darkest  = new Color(new Rgba32((byte)(p.R * 0.3f), (byte)(p.G * 0.3f), (byte)(p.B * 0.3f), p.A));
        var darker   = new Color(new Rgba32((byte)(p.R * 0.7f), (byte)(p.G * 0.7f), (byte)(p.B * 0.7f), p.A));
        var baseC    = accentColor;
        var light    = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.2f), (byte)(p.G + (255 - p.G) * 0.2f), (byte)(p.B + (255 - p.B) * 0.2f), p.A));
        var lighter  = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.4f), (byte)(p.G + (255 - p.G) * 0.4f), (byte)(p.B + (255 - p.B) * 0.4f), p.A));
        var lightest = new Color(new Rgba32((byte)(p.R + (255 - p.R) * 0.6f), (byte)(p.G + (255 - p.G) * 0.6f), (byte)(p.B + (255 - p.B) * 0.6f), p.A));

        (float x, float y, float w, float h, float rx, Color c)[] rects =
        [
            (20, 20, 90, 96, 4, darkest),
            (20, 128, 90, 196, 4, darker),
            (122, 20, 134, 96, 4, baseC),
            (268, 20, 82, 96, 4, light),
            (122, 236, 84, 88, 4, light),
            (218, 236, 132, 88, 4, lighter),
        ];

        foreach (var (rx, ry, rw, rh, rrx, color) in rects)
        {
            var x1 = LeadX(rx);
            var y1 = LeadY(ry);
            var w1 = SnapX(rx + rw) - x1;
            var h1 = SnapY(ry + rh) - y1;
            if (w1 <= 0 || h1 <= 0) continue;
            // Sub-pixel corner radii only blur the corners, so BuildRoundedRect keeps them sharp.
            var cr = Math.Min(rrx * scale, Math.Min(w1, h1) / 2f);
            image.Mutate(ctx => ctx.Fill(color, BuildRoundedRect(x1, y1, w1, h1, cr)));
        }

        float top = LeadY(128), bottom = SnapY(224);
        PointF[] leftPoly = [
            new(LeadX(122), top),
            new(SnapX(256), top),
            new(SnapX(206), bottom),
            new(LeadX(122), bottom),
        ];
        PointF[] rightPoly = [
            new(LeadX(268), top),
            new(SnapX(350), top),
            new(SnapX(350), bottom),
            new(LeadX(218), bottom),
        ];

        image.Mutate(ctx =>
        {
            ctx.Fill(lightest, new Polygon(new LinearLineSegment(leftPoly)));
            ctx.Fill(lighter, new Polygon(new LinearLineSegment(rightPoly)));
        });
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
