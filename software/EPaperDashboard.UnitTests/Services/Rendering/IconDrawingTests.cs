using EPaperDashboard.Services.Rendering;
using FluentAssertions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace EPaperDashboard.UnitTests.Services.Rendering;

public class IconDrawingTests
{
    [Fact]
    public void BuildRoundedRect_ZeroCornerRadius_ReturnsSharpRectangleBounds()
    {
        var path = IconDrawing.BuildRoundedRect(0, 0, 100, 50, cr: 0);

        path.Bounds.Width.Should().Be(100);
        path.Bounds.Height.Should().Be(50);
    }

    [Fact]
    public void BuildRoundedRect_WithCornerRadius_BoundsStillMatchRequestedSize()
    {
        var path = IconDrawing.BuildRoundedRect(0, 0, 100, 50, cr: 10);

        path.Bounds.Width.Should().BeApproximately(100, 1f);
        path.Bounds.Height.Should().BeApproximately(50, 1f);
    }

    [Fact]
    public void BuildRoundedRect_CornerRadiusLargerThanHalfSmallestSide_IsClamped()
    {
        // cr=100 on a 20x20 square should be clamped to 10 (half the smallest side) rather than
        // producing a malformed/self-intersecting path.
        var act = () => IconDrawing.BuildRoundedRect(0, 0, 20, 20, cr: 100);

        act.Should().NotThrow();
    }

    [Fact]
    public void BuildRoundedRect_OffsetPosition_BoundsReflectOffset()
    {
        var path = IconDrawing.BuildRoundedRect(10, 20, 30, 40, cr: 0);

        path.Bounds.Left.Should().Be(10);
        path.Bounds.Top.Should().Be(20);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(128)]
    public void DrawAppIcon_TopRowTiles_AreSeparatedByBlankColumns(int size)
    {
        using var image = RenderAppIcon(size);
        var (top, bottom) = InkRows(image);
        // Middle of the top tile row (20..116 of the 304-unit-tall tile block).
        var y = top + (int)((bottom - top) * 48f / 304f);

        CountInkRuns(x => IsInk(image[x, y]), image.Width).Should().Be(3);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void DrawAppIcon_LeftColumnTiles_AreSeparatedByBlankRows(int size)
    {
        using var image = RenderAppIcon(size);
        var (left, right) = InkColumns(image);
        // Middle of the left tile column (20..110 of the 330-unit-wide tile block).
        var x = left + (int)((right - left) * 45f / 330f);

        CountInkRuns(y => IsInk(image[x, y]), image.Height).Should().Be(2);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    public void DrawAppIcon_TileEdges_AreSnappedToWholePixels(int size)
    {
        using var image = RenderAppIcon(size);
        var (top, bottom) = InkRows(image);
        var y = top + (int)((bottom - top) * 48f / 304f);

        // Top row tiles for a black accent are 0 (darkest/base) and 51 (light); anything else is edge blur.
        for (var x = 0; x < image.Width; x++)
            image[x, y].R.Should().BeOneOf([(byte)0, (byte)51, (byte)255], $"pixel ({x},{y}) should be solid tile or gap");
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(200)]
    public void DrawAppIcon_TileBlock_IsCentredInBounds(int size)
    {
        using var image = RenderAppIcon(size);
        var (top, bottom) = InkRows(image);
        var (left, right) = InkColumns(image);

        (size - 1 - bottom - top).Should().BeInRange(-1, 1);
        (size - 1 - right - left).Should().BeInRange(-1, 1);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(100)]
    [InlineData(128)]
    [InlineData(256)]
    public void AppIconLayout_NeighbouringTiles_AreAllExactlyOneGapApart(int size)
    {
        var shapes = IconDrawing.AppIconLayout(new RectangleF(0, 0, size, size));
        var gap = Math.Max(1f, MathF.Round(12f * size / 370f));

        var distances = new List<float>();
        for (var i = 0; i < shapes.Count; i++)
            for (var j = i + 1; j < shapes.Count; j++)
                distances.Add(Distance(shapes[i].Points, shapes[j].Points));

        // Tiles touching only corner to corner are about 1.4 gaps apart; everything else is a neighbour.
        var neighbours = distances.Where(d => d < gap * 1.3f).ToList();
        neighbours.Should().HaveCount(11);
        neighbours.Should().AllSatisfy(d => d.Should().BeApproximately(gap, 0.01f));
    }

    [Theory]
    [InlineData(16)]
    [InlineData(64)]
    [InlineData(128)]
    public void AppIconLayout_RectangularTiles_HaveWholePixelEdges(int size)
    {
        var shapes = IconDrawing.AppIconLayout(new RectangleF(0, 0, size, size));

        foreach (var point in shapes.Where(s => s.Rounded).SelectMany(s => s.Points))
        {
            point.X.Should().Be(MathF.Round(point.X));
            point.Y.Should().Be(MathF.Round(point.Y));
        }
    }

    // Shortest distance between two non-overlapping convex polygons.
    private static float Distance(PointF[] a, PointF[] b) =>
        Math.Min(a.Min(p => DistanceToPolygon(p, b)), b.Min(p => DistanceToPolygon(p, a)));

    private static float DistanceToPolygon(PointF p, PointF[] polygon) =>
        polygon.Select((start, i) => DistanceToSegment(p, start, polygon[(i + 1) % polygon.Length])).Min();

    private static float DistanceToSegment(PointF p, PointF a, PointF b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0f, 1f);
        var x = a.X + t * dx - p.X;
        var y = a.Y + t * dy - p.Y;
        return MathF.Sqrt(x * x + y * y);
    }

    private static Image<Rgba32> RenderAppIcon(int size)
    {
        var image = new Image<Rgba32>(size, size, Color.White);
        IconDrawing.DrawAppIcon(image, Color.Black, new RectangleF(0, 0, size, size));
        return image;
    }

    // Any tile shade, even the lightest, is well below white.
    private static bool IsInk(Rgba32 p) => p.R < 200;

    private static int CountInkRuns(Func<int, bool> isInk, int length)
    {
        var runs = 0;
        var inRun = false;
        for (var i = 0; i < length; i++)
        {
            var ink = isInk(i);
            if (ink && !inRun) runs++;
            inRun = ink;
        }
        return runs;
    }

    private static (int First, int Last) InkRows(Image<Rgba32> image)
    {
        var rows = Enumerable.Range(0, image.Height)
            .Where(y => Enumerable.Range(0, image.Width).Any(x => IsInk(image[x, y]))).ToList();
        return (rows.First(), rows.Last());
    }

    private static (int First, int Last) InkColumns(Image<Rgba32> image)
    {
        var cols = Enumerable.Range(0, image.Width)
            .Where(x => Enumerable.Range(0, image.Height).Any(y => IsInk(image[x, y]))).ToList();
        return (cols.First(), cols.Last());
    }
}
