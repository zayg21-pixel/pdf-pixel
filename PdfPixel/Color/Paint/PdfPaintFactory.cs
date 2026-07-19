using PdfPixel.Commands;
using PdfPixel.Rendering.State;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Factory for creating SkiaSharp paint objects and typefaces for PDF rendering
/// Enhanced with transparency and blend mode support
/// </summary>
internal static class PdfPaintFactory
{
    /// <summary>
    /// Common initialization shared by all paints.
    /// Sets the blend mode from graphics state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPaint CreateBasePaint(PdfGraphicsState state)
    {
        SKPaint paint = new()
        {
            // Default blend is Normal (SrcOver). Map gstate blend to Skia.
            BlendMode = SkiaEnumUtilities.ToSkiaBlendMode(state.BlendMode)
        };
        return paint;
    }

    /// <summary>
    /// Create a font object for text shaping and measurement.
    /// </summary>
    /// <param name="typeface">Typeface to use</param>
    /// <returns>Configured SKFont</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKFont CreateTextFont(SKTypeface typeface)
    {
        SKFont font = new()
        {
            Typeface = typeface,
            Size = 1,
            Subpixel = true,
            LinearMetrics = true,
            Hinting = SKFontHinting.Slight
        };

        // Skew/rotation are already represented in the text matrix applied at draw time.
        return font;
    }

    /// <summary>
    /// Create a paint object for stroke operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateStrokePaint(PdfGraphicsState state)
    {
        SKPaint paint = CreateBasePaint(state);
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = ApplyAlpha(state.StrokePaint.Color, state.StrokeAlpha).ToSkiaColor();

        ApplyStrokeStyling(paint, state);

        return paint;
    }

    /// <summary>
    /// Create a paint object for fill operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateFillPaint(PdfGraphicsState state)
    {
        SKPaint paint = CreateBasePaint(state);
        paint.Style = SKPaintStyle.Fill;

        paint.Color = ApplyAlpha(state.FillPaint.Color, state.FillAlpha).ToSkiaColor();

        return paint;
    }

    /// <summary>
    /// Layer paint for soft mask.
    /// Antialiasing is deferred to command Execute.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateMaskLayerPaint() => new();

    /// <summary>
    /// Layer paint for composition operations, all blending and composing operations are delegated to layer.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateCompositionLayerPaint(PdfGraphicsState state)
    {
        SKPaint basePaint = CreateBasePaint(state);
        basePaint.Color = ApplyAlpha(SKColors.White, state.FillAlpha);
        return basePaint;
    }

    /// <summary>
    /// Creates default background paint.
    /// Antialiasing is deferred to command Execute time via <see cref="PdfPixel.Commands.PdfCommandExecutionContext"/>.
    /// </summary>
    /// <param name="background">Background color.</param>
    public static SKPaint CreateBackgroundPaint(in PdfColor background)
    {
        return new()
        {
            Style = SKPaintStyle.Fill,
            Color = background.ToSkiaColor()
        };
    }

    /// <summary>
    /// Create paint for mask application (DstIn blend mode).
    /// Antialiasing is deferred to command Execute time via <see cref="PdfPixel.Commands.PdfCommandExecutionContext"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateMaskPaint() => new() { BlendMode = SKBlendMode.DstIn };

    /// <summary>
    /// Creates a basic shader paint with fill style.
    /// Antialiasing is deferred to command Execute time via <see cref="PdfPixel.Commands.PdfCommandExecutionContext"/>.
    /// </summary>
    public static SKPaint CreateShaderPaint() => new() { Style = SKPaintStyle.Fill };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyStrokeStyling(SKPaint paint, PdfGraphicsState state)
    {
        // NOTE (PDF spec): setlinewidth 0 means a device-dependent hairline. Skia interprets
        // StrokeWidth = 0 as a hairline, so pass through 0 unchanged; clamp negatives to 0.
        float width = state.LineWidth;
        paint.StrokeWidth = (width <= 0) ? 0f : width;
        paint.StrokeCap = state.LineCap;
        paint.StrokeJoin = state.LineJoin;
        // Miter limit must be positive; clamp to a safe minimum to avoid Skia issues.
        paint.StrokeMiter = (state.MiterLimit > 0) ? state.MiterLimit : 1f;

        if (state.DashPattern?.Length > 0)
        {
            paint.PathEffect = SKPathEffect.CreateDash(state.DashPattern, state.DashPhase);
        }
    }

    /// <summary>
    /// Sets font edging based on the antialias flag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyAntialias(SKFont font, bool antialias) => font.Edging = antialias ? SKFontEdging.SubpixelAntialias : SKFontEdging.Alias;

    /// <summary>
    /// Creates a deep copy of the given font, preserving all properties.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKFont CloneFont(SKFont font)
    {
        return new()
        {
            Typeface = font.Typeface,
            BaselineSnap = font.BaselineSnap,
            Edging = font.Edging,
            EmbeddedBitmaps = font.EmbeddedBitmaps,
            Embolden = font.Embolden,
            ForceAutoHinting = font.ForceAutoHinting,
            Hinting = font.Hinting,
            LinearMetrics = font.LinearMetrics,
            ScaleX = font.ScaleX,
            Size = font.Size,
            Subpixel = font.Subpixel,
            SkewX = font.SkewX
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor ApplyAlpha(in SKColor color, float alpha)
    {
        // Clamp alpha to valid range
        alpha = Math.Max(0f, Math.Min(1f, alpha));

        // Convert to byte and apply to the color's alpha channel
        var alphaBytes = (byte)(alpha * 255);

        return color.WithAlpha(alphaBytes);
    }

    /// <summary>
    /// Applies the specified alpha (clamped to [0, 1]) to the color's alpha channel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfColor ApplyAlpha(in PdfColor color, float alpha) => color.WithAlpha(Math.Max(0f, Math.Min(1f, alpha)));
}
