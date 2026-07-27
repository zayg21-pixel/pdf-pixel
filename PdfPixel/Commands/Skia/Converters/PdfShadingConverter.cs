using PdfPixel.Color;
using PdfPixel.Commands.Skia;
using PdfPixel.Geometry;
using PdfPixel.Shading.Model;
using SkiaSharp;

namespace PdfPixel.Commands.Converters;

/// <summary>
/// Converts <see cref="PdfPixel.Shading"/> model types to their SkiaSharp equivalents for canvas rendering.
/// </summary>
internal static class PdfShadingConverter
{
    /// <summary>
    /// Builds an <see cref="SKVertices"/> equivalent to <paramref name="vertices"/>.
    /// </summary>
    public static SKVertices ToSkVertices(PdfVertices vertices)
    {
        var positions = new SKPoint[vertices.Positions.Length];
        for (int index = 0; index < positions.Length; index++)
        {
            positions[index] = vertices.Positions[index].ToSkPoint();
        }

        var colors = new SKColor[vertices.Colors.Length];
        for (int index = 0; index < colors.Length; index++)
        {
            colors[index] = vertices.Colors[index].ToSkiaColor();
        }

        return (vertices.Indices != null)
            ? SKVertices.CreateCopy(SKVertexMode.Triangles, positions, null, colors, vertices.Indices)
            : SKVertices.CreateCopy(SKVertexMode.Triangles, positions, colors);
    }

    /// <summary>
    /// Builds the shader paint for an axial (Type 2) gradient.
    /// </summary>
    internal static SKPaint ToSkiaPaint(PdfLinearGradient gradient)
    {
        using SKShader shader = SKShader.CreateLinearGradient(
            gradient.Start.ToSkPoint(),
            gradient.End.ToSkPoint(),
            ToSkColors(gradient.Colors),
            gradient.Positions,
            SKShaderTileMode.Clamp);

        return new SKPaint { Shader = shader };
    }

    /// <summary>
    /// Builds the outer-cone shader paint for a radial (Type 3) gradient.
    /// </summary>
    internal static SKPaint ToSkiaOuterPaint(PdfRadialGradient gradient)
        => BuildRadialPaint(gradient.Center0, gradient.Radius0, gradient.Center1, gradient.Radius1, gradient.Colors, gradient.Positions);

    /// <summary>
    /// Builds the inner-cone shader paint for a radial (Type 3) gradient: the same gradient with
    /// its two circles and color stops reversed.
    /// </summary>
    internal static SKPaint ToSkiaInnerPaint(PdfRadialGradient gradient)
    {
        int count = gradient.Colors.Length;
        var reversedColors = new PdfColor[count];
        var reversedPositions = new float[count];

        for (int i = 0; i < count; i++)
        {
            int sourceIndex = count - 1 - i;
            reversedColors[i] = gradient.Colors[sourceIndex];
            reversedPositions[i] = 1 - gradient.Positions[sourceIndex];
        }

        return BuildRadialPaint(gradient.Center1, gradient.Radius1, gradient.Center0, gradient.Radius0, reversedColors, reversedPositions);
    }

    private static SKPaint BuildRadialPaint(in PdfPoint center0, float radius0, in PdfPoint center1, float radius1, PdfColor[] colors, float[] positions)
    {
        using SKShader shader = SKShader.CreateTwoPointConicalGradient(
            center0.ToSkPoint(),
            radius0,
            center1.ToSkPoint(),
            radius1,
            ToSkColors(colors),
            positions,
            SKShaderTileMode.Clamp);

        return new SKPaint { Shader = shader };
    }

    private static SKColor[] ToSkColors(PdfColor[] colors)
    {
        var result = new SKColor[colors.Length];
        for (int i = 0; i < colors.Length; i++)
        {
            result[i] = colors[i].ToSkiaColor();
        }

        return result;
    }
}
