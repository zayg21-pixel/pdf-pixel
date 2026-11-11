using PdfPixel.Geometry;
using PdfPixel.Snap.Rasterization;
using Xunit;

namespace PdfPixel.Tests.Snap;

/// <summary>
/// Tests for the Snap edge builder and rasterizer: coverage, the fill rules, how abutting shapes share
/// their pixels, and the grid snapping rule.
/// </summary>
public class SnapRasterizationTests
{
    private const float Tolerance = 1e-4f;
    private static readonly PdfIntegerRectangle Limits = new(0, 0, 64, 64);

    [Fact]
    public void AreaCoverage_HalfCoveredPixels_AreHalf()
    {
        SnapCoverage coverage = Rasterize(Rectangle(2.5f, 2f, 6.5f, 4f), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: false);

        Assert.Equal(0.5f, coverage[2, 2], Tolerance);
        Assert.Equal(1f, coverage[3, 2], Tolerance);
        Assert.Equal(0.5f, coverage[6, 3], Tolerance);
    }

    [Fact]
    public void AreaCoverage_EvenOddRule_LeavesInnerSquareEmpty()
    {
        PdfPathBuilder builder = new();
        builder.AddRect(new PdfRectangle(0, 0, 10, 10));
        builder.AddRect(new PdfRectangle(3, 3, 7, 7));

        SnapCoverage evenOdd = Rasterize(builder.ToPath(PdfPathFillType.EvenOdd), PdfPathFillType.EvenOdd, areaCoverage: true, snapToGrid: false);
        SnapCoverage winding = Rasterize(builder.ToPath(PdfPathFillType.Winding), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: false);

        Assert.Equal(0f, evenOdd[5, 5], Tolerance);
        Assert.Equal(1f, evenOdd[1, 1], Tolerance);
        Assert.Equal(1f, winding[5, 5], Tolerance);
    }

    [Fact]
    public void PointSampling_TrianglesSharingADiagonal_SplitEveryPixelExactlyOnce()
    {
        PdfPathBuilder lower = new();
        lower.MoveTo(0, 0);
        lower.LineTo(17.3f, 0);
        lower.LineTo(17.3f, 13.7f);
        lower.Close();

        PdfPathBuilder upper = new();
        upper.MoveTo(0, 0);
        upper.LineTo(17.3f, 13.7f);
        upper.LineTo(0, 13.7f);
        upper.Close();

        float[,] total = new float[64, 64];
        Accumulate(total, Rasterize(lower.ToPath(), PdfPathFillType.Winding, areaCoverage: false, snapToGrid: false));
        Accumulate(total, Rasterize(upper.ToPath(), PdfPathFillType.Winding, areaCoverage: false, snapToGrid: false));

        for (int y = 0; y < 13; y++)
        {
            for (int x = 0; x < 17; x++)
            {
                Assert.Equal(1f, total[x, y]);
            }
        }
    }

    [Fact]
    public void AreaCoverage_TrianglesSharingADiagonal_SumToFullCoverage()
    {
        PdfPathBuilder lower = new();
        lower.MoveTo(0, 0);
        lower.LineTo(16, 0);
        lower.LineTo(16, 12);
        lower.Close();

        PdfPathBuilder upper = new();
        upper.MoveTo(0, 0);
        upper.LineTo(16, 12);
        upper.LineTo(0, 12);
        upper.Close();

        float[,] total = new float[64, 64];
        Accumulate(total, Rasterize(lower.ToPath(), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: false));
        Accumulate(total, Rasterize(upper.ToPath(), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: false));

        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                Assert.Equal(1f, total[x, y], Tolerance);
            }
        }
    }

    [Fact]
    public void Snapping_PutsAxisAlignedEdgesOnPixelBoundaries()
    {
        SnapCoverage coverage = Rasterize(Rectangle(10.3f, 5.4f, 20.6f, 9.5f), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: true);

        Assert.Equal(new PdfIntegerRectangle(10, 5, 21, 10), coverage.Bounds);
        Assert.Equal(1f, coverage[10, 5], Tolerance);
        Assert.Equal(1f, coverage[20, 9], Tolerance);
    }

    [Fact]
    public void Snapping_AbuttingRectangles_KeepSharingTheirEdge()
    {
        SnapCoverage left = Rasterize(Rectangle(2.2f, 0, 7.5f, 4), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: true);
        int leftRight = left.Bounds.Right;
        SnapCoverage right = Rasterize(Rectangle(7.5f, 0, 12.8f, 4), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: true);

        Assert.Equal(leftRight, right.Bounds.Left);
        Assert.Equal(1f, right[right.Bounds.Left, 1], Tolerance);
    }

    [Fact]
    public void Snapping_ThinRectangle_KeepsOnePixel()
    {
        SnapCoverage coverage = Rasterize(Rectangle(4.6f, 2, 4.9f, 30), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: true);

        Assert.Equal(1, coverage.Bounds.Width);
        Assert.Equal(4, coverage.Bounds.Left);
        Assert.Equal(1f, coverage[4, 10], Tolerance);
    }

    [Fact]
    public void Snapping_LeavesDiagonalEdgesAlone()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(2.3f, 2.3f);
        builder.LineTo(20.7f, 9.1f);
        builder.LineTo(2.3f, 17.2f);
        builder.Close();

        SnapCoverage snapped = Rasterize(builder.ToPath(), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: true);
        SnapCoverage unsnapped = Rasterize(builder.ToPath(), PdfPathFillType.Winding, areaCoverage: true, snapToGrid: false);

        Assert.Equal(unsnapped[15, 9], snapped[15, 9], Tolerance);
    }

    private static PdfPath Rectangle(float left, float top, float right, float bottom)
    {
        PdfPathBuilder builder = new();
        builder.AddRect(new PdfRectangle(left, top, right, bottom));

        return builder.ToPath();
    }

    private static SnapCoverage Rasterize(PdfPath path, PdfPathFillType fillType, bool areaCoverage, bool snapToGrid)
    {
        SnapEdgeBuilder edgeBuilder = new();
        SnapEdgeList edges = new();
        edgeBuilder.Build(path, PdfMatrix.Identity, snapToGrid, edges);

        SnapRasterizer rasterizer = new();
        SnapCoverage coverage = rasterizer.Rasterize(edges, fillType, areaCoverage, Limits);

        // The values belong to the rasterizer; copy them so the test owns them.
        return new SnapCoverage(coverage.Bounds, (float[])coverage.Values.Clone());
    }

    private static void Accumulate(float[,] total, SnapCoverage coverage)
    {
        for (int y = coverage.Bounds.Top; y < coverage.Bounds.Bottom; y++)
        {
            for (int x = coverage.Bounds.Left; x < coverage.Bounds.Right; x++)
            {
                total[x, y] += coverage[x, y];
            }
        }
    }
}
