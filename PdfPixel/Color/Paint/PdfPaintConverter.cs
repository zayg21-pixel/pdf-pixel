using PdfPixel.Commands.Converters;
using PdfPixel.Geometry;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Converts a <see cref="PdfPaint"/> to an <see cref="SKPaint"/>. Single place where a paint's color,
/// alpha, blend mode, and stroke styling are translated to their Skia equivalents.
/// </summary>
internal static class PdfPaintConverter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint ToSkiaPaint(PdfPaint paint)
    {
        SKPaint skPaint = new()
        {
            BlendMode = SkiaEnumUtilities.ToSkiaBlendMode(paint.BlendMode),
            Style = (paint.Style == PdfPaintStyle.Stroke) ? SKPaintStyle.Stroke : SKPaintStyle.Fill,
            Color = ApplyAlpha(paint.Color, paint.Alpha).ToSkiaColor()
        };

        if (paint.Style == PdfPaintStyle.Stroke)
        {
            ApplyStrokeStyling(skPaint, paint.RequireStrokeStyle());
        }

        return skPaint;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyStrokeStyling(SKPaint paint, PdfStrokeStyle style)
    {
        // NOTE (PDF spec): setlinewidth 0 means a device-dependent hairline. Skia interprets
        // StrokeWidth = 0 as a hairline, so pass through 0 unchanged; clamp negatives to 0.
        float width = style.LineWidth;
        paint.StrokeWidth = (width <= 0) ? 0f : width;
        paint.StrokeCap = SkiaEnumUtilities.ToSkiaStrokeCap(style.LineCap);
        paint.StrokeJoin = SkiaEnumUtilities.ToSkiaStrokeJoin(style.LineJoin);
        // Miter limit must be positive; clamp to a safe minimum to avoid Skia issues.
        paint.StrokeMiter = (style.MiterLimit > 0) ? style.MiterLimit : 1f;

        if (style.DashPattern?.Length > 0)
        {
            paint.PathEffect = SKPathEffect.CreateDash(style.DashPattern, style.DashPhase);
        }

        if (style.BorderStyleType == PdfBorderStyleType.Beveled || style.BorderStyleType == PdfBorderStyleType.Inset)
        {
            paint.ImageFilter = ComposeFilter(CreateBorderShadowFilter(style.BorderStyleType, width), paint.ImageFilter);
        }

        if (style.BorderEffectType == PdfBorderEffectType.Cloudy)
        {
            paint.PathEffect = ComposeEffect(CreateCloudyEffect(width, style.BorderEffectIntensity), paint.PathEffect);
        }
    }

    private static SKImageFilter CreateBorderShadowFilter(PdfBorderStyleType borderStyleType, float width)
    {
        float shadowOffset = width * 0.5f;
        if (borderStyleType == PdfBorderStyleType.Inset)
        {
            shadowOffset = -shadowOffset;
        }

        return SKImageFilter.CreateDropShadow(
            dx: shadowOffset,
            dy: -shadowOffset,
            sigmaX: width * 0.3f,
            sigmaY: width * 0.3f,
            color: SKColors.Black.WithAlpha(80));
    }

    private static SKPathEffect CreateCloudyEffect(float width, float intensity)
    {
        float bumpRadius = width * (1.5f + intensity * 1.0f);
        float advance = bumpRadius * 1.6f;

        PdfPath bumpPath = new();
        bumpPath.AddArc(new PdfRectangle(-bumpRadius, -bumpRadius, bumpRadius, bumpRadius), 0, -180);
        SKPath bump = bumpPath.ToSkPath();

        return SKPathEffect.Create1DPath(bump, advance, 0, SKPath1DPathEffectStyle.Rotate);
    }

    private static SKImageFilter ComposeFilter(SKImageFilter outer, SKImageFilter? existing)
        => (existing != null) ? SKImageFilter.CreateCompose(outer, existing) : outer;

    private static SKPathEffect ComposeEffect(SKPathEffect outer, SKPathEffect? existing)
        => (existing != null) ? SKPathEffect.CreateCompose(outer, existing) : outer;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfColor ApplyAlpha(in PdfColor color, float alpha) => color.WithAlpha(Math.Max(0f, Math.Min(1f, alpha)));
}
