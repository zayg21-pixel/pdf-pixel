using PdfPixel.Geometry;
using PdfPixel.Snap.Transparency;
using PdfPixel.Transparency;
using Xunit;

namespace PdfPixel.Tests.Snap;

/// <summary>
/// Tests for the group compositing of ISO 32000-2 11.4.8 as <see cref="SnapLayer"/> applies it: elementary
/// objects, isolated and non-isolated groups, knockout groups, and factoring the backdrop out of a group.
/// </summary>
public class SnapGroupCompositingTests
{
    private const float Tolerance = 1e-5f;
    private static readonly PdfIntegerRectangle Pixel = new(0, 0, 1, 1);

    [Fact]
    public void Element_Normal_IsSourceOver()
    {
        SnapLayer page = CreatePage([0.2f, 0.4f, 0.6f]);

        Composite(page, [1, 0, 0], 1, 0.5f, PdfBlendMode.Normal);

        AssertResult(page, [0.6f, 0.2f, 0.3f], 1, 1);
    }

    [Fact]
    public void Element_PartialShape_WeighsLikeOpacity()
    {
        SnapLayer page = CreatePage([0, 0, 1]);

        Composite(page, [1, 0, 0], 0.25f, 0.25f, PdfBlendMode.Normal);

        AssertResult(page, [0.25f, 0, 0.75f], 1, 1);
    }

    [Fact]
    public void Element_Multiply_MultipliesOpaqueBackdrop()
    {
        SnapLayer page = CreatePage([0.5f, 1, 0.2f]);

        Composite(page, [0.5f, 0.5f, 1], 1, 1, PdfBlendMode.Multiply);

        AssertResult(page, [0.25f, 0.5f, 0.2f], 1, 1);
    }

    [Fact]
    public void NonIsolatedGroup_BlendsWithItsBackdrop()
    {
        SnapLayer page = CreatePage([0.5f, 1, 0.2f]);
        SnapLayer group = new(Pixel, SnapBlendingSpace.Rgb, isolated: false, knockout: false, page);

        Composite(group, [0.5f, 0.5f, 1], 1, 1, PdfBlendMode.Multiply);
        CompositeInto(page, group, 1);

        AssertResult(page, [0.25f, 0.5f, 0.2f], 1, 1);
    }

    [Fact]
    public void IsolatedGroup_DoesNotBlendWithItsBackdrop()
    {
        SnapLayer page = CreatePage([0.5f, 1, 0.2f]);
        SnapLayer group = new(Pixel, SnapBlendingSpace.Rgb, isolated: true, knockout: false, page);

        Composite(group, [0.5f, 0.5f, 1], 1, 1, PdfBlendMode.Multiply);
        CompositeInto(page, group, 1);

        AssertResult(page, [0.5f, 0.5f, 1], 1, 1);
    }

    [Fact]
    public void NonIsolatedGroup_FactorsItsBackdropOut()
    {
        SnapLayer page = CreatePage([0, 0, 1]);
        SnapLayer group = new(Pixel, SnapBlendingSpace.Rgb, isolated: false, knockout: false, page);

        Composite(group, [1, 0, 0], 1, 0.4f, PdfBlendMode.Normal);

        var color = new float[3];
        (float alpha, float shape) = GetResult(group, color);

        Assert.Equal(0.4f, alpha, Tolerance);
        Assert.Equal(1f, shape, Tolerance);
        Assert.Equal(1f, color[0], Tolerance);
        Assert.Equal(0f, color[2], Tolerance);

        // Composited back, the group gives what painting its element directly would.
        CompositeInto(page, group, 1);
        AssertResult(page, [0.4f, 0, 0.6f], 1, 1);
    }

    [Fact]
    public void KnockoutGroup_LaterElementReplacesEarlierOne()
    {
        SnapLayer page = CreatePage([1, 1, 1]);
        SnapLayer knockout = new(Pixel, SnapBlendingSpace.Rgb, isolated: true, knockout: true, page);
        SnapLayer ordinary = new(Pixel, SnapBlendingSpace.Rgb, isolated: true, knockout: false, page);

        foreach (SnapLayer group in new[] { knockout, ordinary })
        {
            Composite(group, [1, 0, 0], 1, 0.5f, PdfBlendMode.Normal);
            Composite(group, [0, 0, 1], 1, 0.5f, PdfBlendMode.Normal);
        }

        var color = new float[3];

        Assert.Equal(0.5f, GetResult(knockout, color).Alpha, Tolerance);
        Assert.Equal(0f, color[0], Tolerance);
        Assert.Equal(1f, color[2], Tolerance);

        Assert.Equal(0.75f, GetResult(ordinary, color).Alpha, Tolerance);
    }

    [Fact]
    public void KnockoutGroup_PartialShape_KnocksOutInProportion()
    {
        SnapLayer page = CreatePage([1, 1, 1]);
        SnapLayer group = new(Pixel, SnapBlendingSpace.Rgb, isolated: true, knockout: true, page);

        Composite(group, [1, 0, 0], 1, 1, PdfBlendMode.Normal);
        Composite(group, [0, 0, 1], 0.5f, 0.5f, PdfBlendMode.Normal);

        var color = new float[3];

        Assert.Equal(1f, GetResult(group, color).Alpha, Tolerance);
        Assert.Equal(0.5f, color[0], Tolerance);
        Assert.Equal(0.5f, color[2], Tolerance);
    }

    [Fact]
    public void SoftMask_ScalesTheGroupOpacityOnly()
    {
        SnapLayer page = CreatePage([0, 0, 0]);
        SnapLayer group = new(Pixel, SnapBlendingSpace.Rgb, isolated: true, knockout: false, page);

        Composite(group, [1, 1, 1], 1, 1, PdfBlendMode.Normal);
        group.ApplyMaskRow(0, 0, [0.25f]);

        var color = new float[3];
        (float alpha, float shape) = GetResult(group, color);

        Assert.Equal(0.25f, alpha, Tolerance);
        Assert.Equal(1f, shape, Tolerance);
        Assert.Equal(1f, color[0], Tolerance);
    }

    private static SnapLayer CreatePage(float[] opaqueColor)
    {
        SnapLayer page = new(Pixel, SnapBlendingSpace.Rgb, isolated: true, knockout: false, parent: null);
        Composite(page, opaqueColor, 1, 1, PdfBlendMode.Normal);

        return page;
    }

    private static void Composite(SnapLayer layer, float[] color, float shape, float alpha, PdfBlendMode blendMode)
        => layer.CompositeRow(0, 0, color, 3, [shape], [alpha], blendMode);

    private static (float Alpha, float Shape) GetResult(SnapLayer layer, float[] color)
    {
        var alphas = new float[1];
        var shapes = new float[1];
        layer.GetResultRow(0, 0, color, alphas, shapes);

        return (alphas[0], shapes[0]);
    }

    private static void CompositeInto(SnapLayer parent, SnapLayer group, float constantAlpha)
    {
        var color = new float[3];
        (float alpha, float shape) = GetResult(group, color);
        Composite(parent, color, shape, alpha * constantAlpha, PdfBlendMode.Normal);
    }

    private static void AssertResult(SnapLayer layer, float[] expectedColor, float expectedAlpha, float expectedShape)
    {
        var color = new float[3];
        (float alpha, float shape) = GetResult(layer, color);

        Assert.Equal(expectedAlpha, alpha, Tolerance);
        Assert.Equal(expectedShape, shape, Tolerance);

        for (int component = 0; component < 3; component++)
        {
            Assert.Equal(expectedColor[component], color[component], Tolerance);
        }
    }
}
