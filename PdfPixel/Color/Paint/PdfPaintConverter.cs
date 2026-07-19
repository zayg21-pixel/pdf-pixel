using PdfPixel.Commands;
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
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfColor ApplyAlpha(in PdfColor color, float alpha) => color.WithAlpha(Math.Max(0f, Math.Min(1f, alpha)));
}
