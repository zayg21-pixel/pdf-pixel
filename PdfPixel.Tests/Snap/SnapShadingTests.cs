using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.Shading;
using PdfPixel.Snap.Shading;
using Xunit;

namespace PdfPixel.Tests.Snap;

/// <summary>
/// Tests for the shading parameters Snap evaluates per pixel.
/// </summary>
public class SnapShadingTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Axial_OutsideTheAxis_PaintsOnlyWhereExtended()
    {
        PdfAxialGradient notExtended = CreateAxial(extendStart: false, extendEnd: false);
        PdfAxialGradient extended = CreateAxial(extendStart: true, extendEnd: true);

        Assert.Null(SnapShadingGeometry.GetAxialParameter(notExtended, new PdfPoint(-5, 0)));
        Assert.Equal(0f, SnapShadingGeometry.GetAxialParameter(extended, new PdfPoint(-5, 0)));
        Assert.Equal(1f, SnapShadingGeometry.GetAxialParameter(extended, new PdfPoint(25, 3)));
        Assert.Equal(0.25f, SnapShadingGeometry.GetAxialParameter(notExtended, new PdfPoint(2.5f, 7)) ?? -1, Tolerance);
    }

    [Fact]
    public void Radial_Nested_GivesTheCircleThroughThePoint()
    {
        PdfRadialGradient gradient = CreateRadial(new PdfPoint(0, 0), 10, new PdfPoint(0, 0), 50, extendStart: false, extendEnd: false);

        Assert.Equal(0.5f, SnapShadingGeometry.GetRadialParameter(gradient, new PdfPoint(30, 0)) ?? -1, Tolerance);
        Assert.Null(SnapShadingGeometry.GetRadialParameter(gradient, new PdfPoint(5, 0)));
        Assert.Null(SnapShadingGeometry.GetRadialParameter(gradient, new PdfPoint(60, 0)));
    }

    [Fact]
    public void Radial_ExtendedStart_FillsTheInnerCircle()
    {
        PdfRadialGradient gradient = CreateRadial(new PdfPoint(0, 0), 10, new PdfPoint(0, 0), 50, extendStart: true, extendEnd: false);

        Assert.Equal(0f, SnapShadingGeometry.GetRadialParameter(gradient, new PdfPoint(5, 0)));
    }

    [Fact]
    public void Radial_Cone_PaintsNothingOutsideItWithoutExtension()
    {
        // Two separate circles: the shading is the cone between them, empty beside it.
        PdfRadialGradient gradient = CreateRadial(new PdfPoint(0, 0), 5, new PdfPoint(100, 0), 10, extendStart: false, extendEnd: false);

        Assert.NotNull(SnapShadingGeometry.GetRadialParameter(gradient, new PdfPoint(50, 0)));
        Assert.Null(SnapShadingGeometry.GetRadialParameter(gradient, new PdfPoint(50, 40)));
    }

    [Fact]
    public void Radial_LargerCircleWins_WhereCirclesOverlap()
    {
        PdfRadialGradient gradient = CreateRadial(new PdfPoint(0, 0), 20, new PdfPoint(10, 0), 20, extendStart: false, extendEnd: false);

        // Every circle of the family has radius 20; (25, 0) is on the circles with s = 0.5 only once x − 10s = ±20.
        Assert.Equal(0.5f, SnapShadingGeometry.GetRadialParameter(gradient, new PdfPoint(25, 0)) ?? -1, Tolerance);
    }

    private static PdfAxialGradient CreateAxial(bool extendStart, bool extendEnd)
        => new(new PdfPoint(0, 0), new PdfPoint(10, 0), [Gray(0), Gray(1)], [0f, 1f], extendStart, extendEnd);

    private static PdfRadialGradient CreateRadial(in PdfPoint center0, float radius0, in PdfPoint center1, float radius1, bool extendStart, bool extendEnd)
        => new(center0, radius0, center1, radius1, [Gray(0), Gray(1)], [0f, 1f], extendStart, extendEnd);

    private static PdfColor Gray(float value) => new([value]);
}
