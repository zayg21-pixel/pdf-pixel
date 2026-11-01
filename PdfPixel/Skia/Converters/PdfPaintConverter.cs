using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Geometry;
using PdfPixel.Skia.Cache;
using PdfPixel.Transparency;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Skia.Converters;

/// <summary>
/// Converts a <see cref="PdfPaint"/> to an <see cref="SKPaint"/>. Single place where a paint's color,
/// alpha and blend mode are translated to their Skia equivalents.
/// </summary>
internal static class PdfPaintConverter
{
    private const int TransferFunctionTableSize = 256;

    /// <summary>
    /// Converts <paramref name="paint"/> to a fill paint, converting its color with <paramref name="colorSampler"/>.
    /// A stroke reaches Skia as an outline that <see cref="Geometry.PathOperations.PdfStrokeOutlineBuilder"/> has
    /// already widened, dashed, capped, and joined, so every paint fills.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKPaint ToSkiaPaint(PdfPaint paint, SkiaColorSampler colorSampler)
    {
        SKPaint skPaint = new()
        {
            BlendMode = (paint.MaskParameters != null) ? SKBlendMode.DstIn : paint.BlendMode.ToSkiaBlendMode(),
            Color = colorSampler.ToSkColor(paint)
        };

        if (paint.MaskParameters is { } maskParameters)
        {
            using SKColorFilter? maskFilter = CreateMaskColorFilter(maskParameters);

            if (maskFilter != null)
            {
                skPaint.ColorFilter = maskFilter;
            }
        }

        return skPaint;
    }

    // Converts the mask layer's rendered color into the alpha value it composites with: luminosity
    // masks first fold color into luma (PDF's "gray" mask value), alpha masks use the rendered alpha
    // directly. Either value then passes through the mask's own transfer function (/TR), if any.
    private static SKColorFilter? CreateMaskColorFilter(PdfMaskPaintParameters maskParameters)
    {
        bool isLuminosity = maskParameters.Subtype == PdfSoftMaskSubtype.Luminosity;

        if (maskParameters.TransferFunction == null)
        {
            return isLuminosity ? SKColorFilter.CreateLumaColor() : null;
        }

        SKColorFilter transferFilter = CreateTransferFunctionAlphaFilter(maskParameters.TransferFunction);

        if (!isLuminosity)
        {
            return transferFilter;
        }

        using (SKColorFilter lumaFilter = SKColorFilter.CreateLumaColor())
        using (transferFilter)
        {
            return SKColorFilter.CreateCompose(transferFilter, lumaFilter);
        }
    }

    // Remaps only the alpha channel through the transfer function; color channels pass through unchanged.
    private static SKColorFilter CreateTransferFunctionAlphaFilter(TransferFunctionTransform transferFunction)
    {
        var alphaTable = new byte[TransferFunctionTableSize];

        for (int index = 0; index < TransferFunctionTableSize; index++)
        {
            float input = index / (float)(TransferFunctionTableSize - 1);
            float output = transferFunction.Transform(new Vector4(input, input, input, 0f)).X;
            float scaled = Math.Max(0f, Math.Min(255f, (output * 255f) + 0.5f));
            alphaTable[index] = (byte)scaled;
        }

        return SKColorFilter.CreateTable(alphaTable, IdentityTable, IdentityTable, IdentityTable);
    }

    private static byte[] IdentityTable { get; } = BuildIdentityTable();

    private static byte[] BuildIdentityTable()
    {
        var table = new byte[TransferFunctionTableSize];

        for (int index = 0; index < TransferFunctionTableSize; index++)
        {
            table[index] = (byte)index;
        }

        return table;
    }
}
