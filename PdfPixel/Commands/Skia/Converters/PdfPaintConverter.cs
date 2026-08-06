using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands.Skia;
using PdfPixel.Geometry;
using PdfPixel.Transparency.Model;
using SkiaSharp;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands.Converters;

/// <summary>
/// Converts a <see cref="PdfPaint"/> to an <see cref="SKPaint"/>. Single place where a paint's color,
/// alpha, blend mode, and stroke styling are translated to their Skia equivalents.
/// </summary>
internal static class PdfPaintConverter
{
    private const int TransferFunctionTableSize = 256;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKPaint ToSkiaPaint(PdfPaint paint) => ToSkiaPaint(paint, default);

    /// <summary>
    /// Converts <paramref name="paint"/>, widening its stroke by <paramref name="strokeScale"/>. A pen
    /// that widens alike on both axes is folded into the stroke width outright; one that does not keeps
    /// its authored width, because the caller has to draw the ellipse some other way.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static SKPaint ToSkiaPaint(PdfPaint paint, in PdfStrokeScale strokeScale)
    {
        SKPaint skPaint = new()
        {
            BlendMode = (paint.MaskParameters != null) ? SKBlendMode.DstIn : paint.BlendMode.ToSkiaBlendMode(),
            Style = (paint.Style == PdfPaintStyle.Stroke) ? SKPaintStyle.Stroke : SKPaintStyle.Fill,
            Color = ApplyAlpha(paint.Color, paint.Alpha).ToSkiaColor()
        };

        if (paint.Style == PdfPaintStyle.Stroke)
        {
            ApplyStrokeStyling(skPaint, paint.RequireStrokeStyle(), strokeScale);
        }

        if (paint.ShadowEffect is { } shadowEffect)
        {
            skPaint.ImageFilter = ComposeFilter(CreateShadowFilter(shadowEffect), skPaint.ImageFilter);
        }

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyStrokeStyling(SKPaint paint, PdfStrokeStyle style, in PdfStrokeScale strokeScale)
    {
        // NOTE (PDF spec): setlinewidth 0 means a device-dependent hairline. Skia interprets
        // StrokeWidth = 0 as a hairline, so pass through 0 unchanged; clamp negatives to 0.
        float width = style.LineWidth;
        paint.StrokeWidth = GetStrokeWidth(width, strokeScale);
        paint.StrokeCap = style.LineCap.ToSkiaStrokeCap();
        paint.StrokeJoin = style.LineJoin.ToSkiaStrokeJoin();
        // Miter limit must be positive; clamp to a safe minimum to avoid Skia issues.
        paint.StrokeMiter = (style.MiterLimit > 0) ? style.MiterLimit : 1f;

        if (style.DashPattern?.Length > 0)
        {
            paint.PathEffect = CreateDashEffect(style, strokeScale);
        }

        if (style.EffectType == PdfStrokeEffectType.Cloudy)
        {
            paint.PathEffect = ComposeEffect(CreateCloudyEffect(width, style.EffectIntensity), paint.PathEffect);
        }
    }

    // A pen that widens alike on both axes is stroked at the widened width. One that does not is drawn
    // from an outline built along a path the caller shrinks first, where the authored width applies.
    private static float GetStrokeWidth(float lineWidth, in PdfStrokeScale strokeScale)
    {
        if (strokeScale.PenWidth <= 0)
        {
            return (lineWidth <= 0) ? 0f : lineWidth;
        }

        return (strokeScale.IsUniform) ? strokeScale.UniformWidth : strokeScale.PenWidth;
    }

    // An anisotropic pen is stroked along a path shrunk by the widening factors, so the dash travels a
    // shorter path and its pattern has to shrink with it.
    private static SKPathEffect CreateDashEffect(PdfStrokeStyle style, in PdfStrokeScale strokeScale)
    {
        float dashScale = (strokeScale.PenWidth <= 0 || strokeScale.IsUniform) ? 1f : Math.Max(strokeScale.X, strokeScale.Y);

        if (dashScale == 1f || style.DashPattern == null)
        {
            return SKPathEffect.CreateDash(style.DashPattern, style.DashPhase);
        }

        var intervals = new float[style.DashPattern.Length];
        for (int index = 0; index < intervals.Length; index++)
        {
            intervals[index] = style.DashPattern[index] / dashScale;
        }

        return SKPathEffect.CreateDash(intervals, style.DashPhase / dashScale);
    }

    private static SKImageFilter CreateShadowFilter(PdfPaintShadowEffect shadowEffect)
    {
        return SKImageFilter.CreateDropShadow(
            dx: shadowEffect.OffsetX,
            dy: shadowEffect.OffsetY,
            sigmaX: shadowEffect.SigmaX,
            sigmaY: shadowEffect.SigmaY,
            color: shadowEffect.Color.ToSkiaColor());
    }

    private static SKPathEffect CreateCloudyEffect(float width, float intensity)
    {
        float bumpRadius = width * (1.5f + intensity * 1.0f);
        float advance = bumpRadius * 1.6f;

        PdfPathBuilder bumpPath = new();
        bumpPath.AddArc(new PdfRectangle(-bumpRadius, -bumpRadius, bumpRadius, bumpRadius), 0, -180);
        SKPath bump = bumpPath.ToPath().ToSkPath();

        return SKPathEffect.Create1DPath(bump, advance, 0, SKPath1DPathEffectStyle.Rotate);
    }

    private static SKImageFilter ComposeFilter(SKImageFilter outer, SKImageFilter? existing)
        => (existing != null) ? SKImageFilter.CreateCompose(outer, existing) : outer;

    private static SKPathEffect ComposeEffect(SKPathEffect outer, SKPathEffect? existing)
        => (existing != null) ? SKPathEffect.CreateCompose(outer, existing) : outer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfColor ApplyAlpha(in PdfColor color, float alpha) => color.WithAlpha(color.Alpha * Math.Max(0f, Math.Min(1f, alpha)));
}
