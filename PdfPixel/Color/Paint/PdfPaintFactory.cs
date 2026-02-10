using PdfPixel.Imaging.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Factory for creating SkiaSharp paint objects and typefaces for PDF rendering
/// Enhanced with transparency and blend mode support
/// </summary>
public static class PdfPaintFactory
{
    /// <summary>
    /// Common initialization shared by all paints.
    /// Sets antialiasing and the blend mode from graphics state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPaint CreateBasePaint(PdfGraphicsState state)
    {
        var paint = new SKPaint
        {
            IsAntialias = !state.RenderingParameters.PreviewMode,
            // Default blend is Normal (SrcOver). Map gstate blend to Skia.
            BlendMode = PdfBlendModeNames.ToSkiaBlendMode(state.BlendMode)
        };
        return paint;
    }

    /// <summary>
    /// Create a font object for text shaping and measurement.
    /// </summary>
    /// <param name="state">Current graphics state</param>
    /// <param name="typeface">Typeface to use</param>
    /// <returns>Configured SKFont</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKFont CreateTextFont(SKTypeface typeface)
    {
        var font = new SKFont
        {
            Typeface = typeface,
            Size = 1,
            // Improve visual quality and keep metrics stable across hinting variations
            Subpixel = true,
            LinearMetrics = true,
            Hinting = SKFontHinting.Normal,
            Edging = SKFontEdging.SubpixelAntialias
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
        var paint = CreateBasePaint(state);
        paint.Style = SKPaintStyle.Stroke;
        paint.Color = ApplyAlpha(state.StrokePaint.Color, state.StrokeAlpha);

        ApplyStrokeStyling(paint, state);

        return paint;
    }

    /// <summary>
    /// Create a paint object for fill operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateFillPaint(PdfGraphicsState state)
    {
        var paint = CreateBasePaint(state);
        paint.Style = SKPaintStyle.Fill;

        paint.Color = ApplyAlpha(state.FillPaint.Color, state.FillAlpha);

        return paint;
    }

    /// <summary>
    /// Create a paint object for image operations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateImagePaint(PdfGraphicsState state)
    {
        var paint = CreateBasePaint(state);
        paint.IsAntialias = false;

        // For images, we typically use fill alpha since images are considered non-stroking operations
        paint.Color = ApplyAlpha(SKColors.White, state.FillAlpha);

        return paint;
    }

    /// <summary>
    /// Paint for filling masked images (No special blend mode, antiliasing enabled).
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateMaskImagePaint(PdfGraphicsState state)
    {
        return new SKPaint { IsAntialias = !state.RenderingParameters.PreviewMode };
    }

    /// <summary>
    /// Image mask paint (DstIn blend mode).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateImageMaskPaint(PdfGraphicsState state)
    {
        return new SKPaint
        {
            IsAntialias = !state.RenderingParameters.PreviewMode,
            BlendMode = SKBlendMode.DstIn,
        };
    }

    /// <summary>
    /// Image fill for stencil/mask images (from base fill paint, antiliasing enabled).
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateMaskImageFillPaint(PdfGraphicsState state)
    {
        return new SKPaint
        {
            IsAntialias = !state.RenderingParameters.PreviewMode,
            Style = SKPaintStyle.Fill,
            Color = state.FillPaint.Color,
            BlendMode = SKBlendMode.SrcIn,
        };
    }

    /// <summary>
    /// Layer paint for soft mask.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateMaskLayerPaint(PdfGraphicsState state)
    {
        return new SKPaint
        {
            IsAntialias = !state.RenderingParameters.PreviewMode,
        };
    }

    /// <summary>
    /// Layer paint for composition operations, all blending and composing operations are delegated to layer.
    /// </summary>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateCompositionLayerPaint(PdfGraphicsState state)
    {
        var basePaint = CreateBasePaint(state);
        basePaint.Color = ApplyAlpha(SKColors.White, state.FillAlpha);
        return basePaint;
    }

    /// <summary>
    /// Creates default background paint.
    /// </summary>
    /// <param name="background">Background color.</param>
    /// <returns></returns>
    public static SKPaint CreateBackgroundPaint(SKColor background, PdfGraphicsState state)
    {
        return new SKPaint
        {
            IsAntialias = !state.RenderingParameters.PreviewMode,
            Style = SKPaintStyle.Fill,
            Color = background
        };
    }

    /// <summary>
    /// Create paint for mask application (DstIn blend mode)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateMaskPaint(PdfGraphicsState state)
    {
        return new SKPaint
        {
            IsAntialias = !state.RenderingParameters.PreviewMode,
            BlendMode = SKBlendMode.DstIn,
        };
    }

    /// <summary>
    /// Creates a basic shader paint with white color and specified antialiasing.
    /// </summary>
    /// <param name="antiAlias">If true, enables antialiasing.</param>
    public static SKPaint CreateShaderPaint(bool antiAlias)
    {
        return new SKPaint
        {
            IsAntialias = antiAlias,
            Style = SKPaintStyle.Fill,
        };
    }

    /// <summary>
    /// Creates shading paint for shading patterns.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateShadingPaint(PdfGraphicsState state)
    {
        var paint = CreateBasePaint(state);
        paint.Color = ApplyAlpha(SKColors.Black, state.FillAlpha);

        return paint;
    }

    /// <summary>
    /// Return Skia sampling options for image and mask rendering based on the PDF image /Interpolate flag
    /// and image type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKSamplingOptions GetImageSamplingOptions(PdfImage image, PdfGraphicsState state)
    {
        if (state.RenderingParameters.GetScaledSize(new SKSizeI(image.Width, image.Height), state.CTM).HasValue
            || state.RenderingParameters.PreviewMode || state.RenderingParameters.ForceImageInterpolation)
        {
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        }

        if (image.Interpolate)
        {
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        }

        return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyStrokeStyling(SKPaint paint, PdfGraphicsState state)
    {
        // NOTE (PDF spec): setlinewidth 0 means a device-dependent hairline. Skia interprets
        // StrokeWidth = 0 as a hairline, so pass through 0 unchanged; clamp negatives to 0.
        var width = state.LineWidth;
        paint.StrokeWidth = width <= 0 ? 0f : width;
        paint.StrokeCap = state.LineCap;
        paint.StrokeJoin = state.LineJoin;
        // Miter limit must be positive; clamp to a safe minimum to avoid Skia issues.
        paint.StrokeMiter = state.MiterLimit > 0 ? state.MiterLimit : 1f;

        if (state.DashPattern != null && state.DashPattern.Length > 0)
        {
            paint.PathEffect = SKPathEffect.CreateDash(state.DashPattern, state.DashPhase);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor ApplyAlpha(SKColor color, float alpha)
    {
        // Clamp alpha to valid range
        alpha = Math.Max(0f, Math.Min(1f, alpha));

        // Convert to byte and apply to the color's alpha channel
        var alphaBytes = (byte)(alpha * 255);

        return color.WithAlpha(alphaBytes);
    }
}