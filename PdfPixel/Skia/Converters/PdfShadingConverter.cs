using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Shading;
using PdfPixel.Skia.Cache;
using SkiaSharp;

namespace PdfPixel.Skia.Converters;

/// <summary>
/// Converts <see cref="PdfPixel.Shading"/> model types to their SkiaSharp equivalents for canvas rendering.
/// </summary>
internal static class PdfShadingConverter
{
    private const int RgbComponentCount = 3;
    private const int ByteBitsPerComponent = 8;

    private static readonly PdfImageChannel[] RgbaChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];

    /// <summary>
    /// Builds an <see cref="SKVertices"/> equivalent to <paramref name="vertices"/>, converting its colors with
    /// <paramref name="colorSampler"/>.
    /// </summary>
    public static SKVertices ToSkVertices(PdfVertices vertices, SkiaColorSampler colorSampler)
    {
        var positions = new SKPoint[vertices.Positions.Length];
        for (int index = 0; index < positions.Length; index++)
        {
            positions[index] = vertices.Positions[index].ToSkPoint();
        }

        var colors = new SKColor[vertices.Colors.Length];
        for (int index = 0; index < colors.Length; index++)
        {
            colors[index] = colorSampler.ToSkColor(vertices.Colors[index]);
        }

        return (vertices.Indices != null)
            ? SKVertices.CreateCopy(SKVertexMode.Triangles, positions, null, colors, vertices.Indices)
            : SKVertices.CreateCopy(SKVertexMode.Triangles, positions, colors);
    }

    /// <summary>
    /// Builds the shader for an axial (Type 2) gradient, converting its colors with <paramref name="colorSampler"/>.
    /// </summary>
    internal static SKShader ToSkiaShader(PdfAxialGradient gradient, SkiaColorSampler colorSampler)
    {
        (SKColor[] colors, float[] positions) = ToSkStops(gradient.Colors, gradient.Positions, gradient.ExtendStart, gradient.ExtendEnd, colorSampler);

        return SKShader.CreateLinearGradient(
            gradient.Start.ToSkPoint(),
            gradient.End.ToSkPoint(),
            colors,
            positions,
            SKShaderTileMode.Clamp);
    }

    /// <summary>
    /// Builds the outer-cone shader for a radial (Type 3) gradient, converting its colors with <paramref name="colorSampler"/>.
    /// </summary>
    internal static SKShader ToSkiaOuterShader(PdfRadialGradient gradient, SkiaColorSampler colorSampler)
    {
        (SKColor[] colors, float[] positions) = ToSkStops(gradient.Colors, gradient.Positions, gradient.ExtendStart, gradient.ExtendEnd, colorSampler);

        return BuildRadialShader(gradient.Center0, gradient.Radius0, gradient.Center1, gradient.Radius1, colors, positions);
    }

    /// <summary>
    /// Builds the inner-cone shader for a radial (Type 3) gradient: the same gradient with
    /// its two circles and color stops reversed, converting its colors with <paramref name="colorSampler"/>.
    /// </summary>
    internal static SKShader ToSkiaInnerShader(PdfRadialGradient gradient, SkiaColorSampler colorSampler)
    {
        (SKColor[] colors, float[] positions) = ToSkStops(gradient.Colors, gradient.Positions, gradient.ExtendStart, gradient.ExtendEnd, colorSampler);

        int count = colors.Length;
        var reversedColors = new SKColor[count];
        var reversedPositions = new float[count];

        for (int i = 0; i < count; i++)
        {
            int sourceIndex = count - 1 - i;
            reversedColors[i] = colors[sourceIndex];
            reversedPositions[i] = 1 - positions[sourceIndex];
        }

        return BuildRadialShader(gradient.Center1, gradient.Radius1, gradient.Center0, gradient.Radius0, reversedColors, reversedPositions);
    }

    // TODO: Build with a shading implementation of IPdfImagePipelineFactory once the factory is split.
    /// <summary>
    /// Builds an <see cref="SKImage"/> of a function-based (Type 1) shading image, converting its colors with
    /// <paramref name="colorSampler"/>.
    /// </summary>
    internal static SKImage ToSkImage(PdfShadingImage image, SkiaColorSampler colorSampler)
    {
        PdfImageSamples source = image.Samples;
        int width = source.Width;

        IPdfImageRowProcessingStep[] steps =
        [
            new PdfImageRowColorStep(colorSampler.Sampler, width, source.Channels.Count, RgbComponentCount, hasAlpha: false),
            new PdfImageRowFloatToByteStep(width, RgbComponentCount, ByteBitsPerComponent),
            new PdfImageRowExpandToRgbaStep(width, RgbComponentCount)
        ];

        PdfImageSamples samples = new(width, source.Height, PdfImageSampleFormat.Integer, ByteBitsPerComponent, RgbaChannels);
        PdfImageRowProcessingPipeline pipeline = new(steps, samples);

        for (int rowIndex = 0; rowIndex < source.Height; rowIndex++)
        {
            pipeline.ProcessRow(source.GetRow(rowIndex), rowIndex);
        }

        return PdfImageConverter.ToSkImage(pipeline.Image);
    }

    private static SKShader BuildRadialShader(in PdfPoint center0, float radius0, in PdfPoint center1, float radius1, SKColor[] colors, float[] positions)
    {
        return SKShader.CreateTwoPointConicalGradient(
            center0.ToSkPoint(),
            radius0,
            center1.ToSkPoint(),
            radius1,
            colors,
            positions,
            SKShaderTileMode.Clamp);
    }

    // A gradient that does not extend past an end gets a transparent stop at that end, so the clamped
    // shader paints nothing beyond it.
    private static (SKColor[] Colors, float[] Positions) ToSkStops(
        PdfColor[] colors,
        float[] positions,
        bool extendStart,
        bool extendEnd,
        SkiaColorSampler colorSampler)
    {
        int startOffset = extendStart ? 0 : 1;
        int count = colors.Length + startOffset + (extendEnd ? 0 : 1);
        var skColors = new SKColor[count];
        var skPositions = new float[count];

        if (!extendStart)
        {
            skColors[0] = SKColors.Transparent;
            skPositions[0] = positions[0];
        }

        for (int index = 0; index < colors.Length; index++)
        {
            skColors[startOffset + index] = colorSampler.ToSkColor(colors[index]);
            skPositions[startOffset + index] = positions[index];
        }

        if (!extendEnd)
        {
            skColors[count - 1] = SKColors.Transparent;
            skPositions[count - 1] = positions[positions.Length - 1];
        }

        return (skColors, skPositions);
    }
}
