using PdfPixel.Rendering.State;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Factory for creating paints for PDF rendering. Produces <see cref="PdfPaint"/> wherever the paint
/// represents PDF color/alpha/blend state; falls back to raw <see cref="SKPaint"/> only for synthetic
/// layer/mask/shader paints that carry no PDF color semantics of their own.
/// </summary>
internal static class PdfPaintFactory
{
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
    /// Layer paint for composition operations, all blending and composing operations are delegated to layer.
    /// Alpha and blend mode come from the current fill paint; the color itself is irrelevant (opaque white)
    /// since the layer only exists to carry compositing parameters.
    /// </summary>
    public static PdfPaint CreateCompositionLayerPaint(PdfGraphicsState state)
    {
        PdfPaint fillPaint = state.FillPaint;
        return new PdfPaint(PdfPaintStyle.Fill)
        {
            Color = PdfColors.White,
            Alpha = fillPaint.Alpha,
            BlendMode = fillPaint.BlendMode
        };
    }

    /// <summary>
    /// Creates default background paint.
    /// Antialiasing is deferred to command Execute time via <see cref="PdfPixel.Commands.PdfCommandExecutionContext"/>.
    /// </summary>
    /// <param name="background">Background color.</param>
    public static PdfPaint CreateBackgroundPaint(in PdfColor background) => PdfPaint.Solid(background, PdfPaintStyle.Fill);

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
}
