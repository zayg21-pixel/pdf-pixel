using PdfReader.Models;
using PdfReader.Rendering.State;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.Paint;

// TODO: Move more base brush/paint creation logic here
/// <summary>
/// Factory for creating SkiaSharp paint objects and typefaces for PDF rendering
/// Enhanced with transparency and blend mode support
/// </summary>
public static class PdfPaintFactory
{
    /// <summary>
    /// Apply alpha transparency from graphics state to an SKColor
    /// </summary>
    /// <param name="color">Base color</param>
    /// <param name="alpha">Alpha value from 0.0 (transparent) to 1.0 (opaque)</param>
    /// <returns>Color with applied alpha</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKColor ApplyAlpha(SKColor color, float alpha)
    {
        // Clamp alpha to valid range
        alpha = Math.Max(0f, Math.Min(1f, alpha));

        // Convert to byte and apply to the color's alpha channel
        var alphaBytes = (byte)(alpha * 255);

        return color.WithAlpha(alphaBytes);
    }

    /// <summary>
    /// Common initialization shared by all paints.
    /// Sets antialiasing and the blend mode from graphics state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SKPaint CreateBasePaint(PdfGraphicsState state)
    {
        var paint = new SKPaint
        {
            IsAntialias = true,
            // Default blend is Normal (SrcOver). Map gstate blend to Skia.
            BlendMode = PdfBlendModeNames.ToSkiaBlendMode(state.BlendMode)
        };
        return paint;
    }

    /// <summary>
    /// Apply stroke styling attributes from graphics state onto a paint.
    /// Implements hairline for zero width per PDF spec and normalizes miter limit.
    /// </summary>
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
    }

    /// <summary>
    /// Normalize dash pattern per PDF spec rules to avoid Skia errors.
    /// - All elements must be nonnegative; replace zeros with a tiny epsilon when any positive exists.
    /// - The sum of elements must be &gt; 0; otherwise, disable dashing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float[] NormalizeDashPattern(float[] pattern)
    {
        if (pattern == null || pattern.Length == 0)
        {
            return null;
        }

        if (pattern.Length % 2 != 0)
        {
            // Per PDF spec, odd-length patterns are repeated to make even-length
            var extended = new float[pattern.Length * 2];
            Array.Copy(pattern, extended, pattern.Length);
            Array.Copy(pattern, 0, extended, pattern.Length, pattern.Length);
            pattern = extended;
        }

        var sum = 0f;
        for (int i = 0; i < pattern.Length; i++)
        {
            var v = pattern[i];
            if (v > 0) sum += v;
        }

        if (sum <= 0f)
        {
            // Per PDF spec, if the sum is zero, the line is solid (no dash effect)
            return null;
        }

        // Skia requires strictly positive intervals; replace zeros with a tiny epsilon
        const float Epsilon = 0.01f; // user-space units; small enough to be visually negligible
        var normalized = new float[pattern.Length];
        for (int i = 0; i < pattern.Length; i++)
        {
            var v = pattern[i];
            normalized[i] = v <= 0 ? Epsilon : v;
        }
        return normalized;
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
    /// Create a paint object for stroke operations with transparency support
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateStrokePaint(PdfGraphicsState state)
    {
        var paint = CreateBasePaint(state);
        paint.Style = SKPaintStyle.Stroke;
        if (state.StrokePaint != null && state.StrokePaint.IsPattern && state.StrokePaint.Pattern != null)
        {
            // Use pattern shader for stroke
            paint.Shader = state.StrokePaint.Pattern.AsShader(state.RenderingIntent, state);
            paint.Color = ApplyAlpha(SKColors.White, state.StrokeAlpha);
        }
        else
        {
            paint.Color = ApplyAlpha(state.StrokePaint.Color, state.StrokeAlpha);
        }

        ApplyStrokeStyling(paint, state);

        if (state.DashPattern != null && state.DashPattern.Length > 0)
        {
            var normalized = NormalizeDashPattern(state.DashPattern);
            if (normalized != null)
            {
                paint.PathEffect = SKPathEffect.CreateDash(normalized, state.DashPhase);
            }
        }

        // Apply advanced transparency effects if present
        ApplyAdvancedTransparencyEffects(paint, state);

        return paint;
    }

    /// <summary>
    /// Create a paint object for fill operations with transparency support
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateFillPaint(PdfGraphicsState state)
    {
        var paint = CreateBasePaint(state);
        paint.Style = SKPaintStyle.Fill;

        if (state.FillPaint != null && state.FillPaint.IsPattern && state.FillPaint.Pattern != null)
        {
            // Use pattern shader for fill
            paint.Shader = state.FillPaint.Pattern.AsShader(state.RenderingIntent, state);
            paint.Color = ApplyAlpha(SKColors.White, state.FillAlpha);
        }
        else
        {
            paint.Color = ApplyAlpha(state.FillPaint.Color, state.FillAlpha);
        }

        // Apply advanced transparency effects if present
        ApplyAdvancedTransparencyEffects(paint, state);

        return paint;
    }

    /// <summary>
    /// Create a paint object for image operations with transparency support
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateImagePaint(PdfGraphicsState state)
    {
        var paint = CreateBasePaint(state);
        // For images, we typically use fill alpha since images are considered non-stroking operations
        paint.Color = ApplyAlpha(SKColors.White, state.FillAlpha);

        // Apply advanced transparency effects if present
        ApplyAdvancedTransparencyEffects(paint, state);

        return paint;
    }

    /// <summary>
    /// Create paint for Form XObject rendering with transparency group support
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateFormXObjectPaint(PdfGraphicsState state)
    {
        var paint = CreateBasePaint(state);
        // NOTE: We use white with FillAlpha to apply overall non-stroking alpha when compositing the form
        // back to the page. The actual group isolation/knockout is handled in PdfXObjectProcessor.
        paint.Color = ApplyAlpha(SKColors.White, state.FillAlpha);
        return paint;
    }

    /// <summary>
    /// Create paint for shadings (axial/radial), applying fill alpha and blend mode without tinting
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint CreateShadingPaint(PdfGraphicsState state, SKShader shader)
    {
        var paint = CreateBasePaint(state);
        paint.Style = SKPaintStyle.Fill;
        // NOTE (PDF spec): The non-stroking alpha (ca) multiplies the result color. To avoid
        // tinting gradient shader colors, we set paint color to white with overall FillAlpha.
        paint.Color = ApplyAlpha(SKColors.White, state.FillAlpha);
        paint.Shader = shader;

        // Apply advanced transparency effects if present
        ApplyAdvancedTransparencyEffects(paint, state);

        return paint;
    }

    /// <summary>
    /// Apply advanced transparency effects including soft masks, knockout, and overprint
    /// This is crucial for rendering shadows and other complex transparency effects correctly
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ApplyAdvancedTransparencyEffects(SKPaint paint, PdfGraphicsState state)
    {
        // Apply knockout effects
        if (state.Knockout)
        {
            // NOTE (Approximation): True knockout requires group compositing semantics. Here we
            // approximate by switching to Src blending when Normal was requested.
            if (paint.BlendMode == SKBlendMode.SrcOver)
            {
                paint.BlendMode = SKBlendMode.Src; // Approximation of knockout
            }
        }
    }

    /// <summary>
    /// Return Skia sampling options for image and mask rendering based on the PDF image /Interpolate flag.
    /// Linear filtering is used when interpolation is requested; otherwise nearest neighbor.
    /// Mipmaps are disabled because PDF images are drawn at explicit resolutions set by the content stream.
    /// </summary>
    /// <param name="interpolate">True if the image dictionary has /Interpolate true.</param>
    /// <returns>Sampling options appropriate for image rendering.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKSamplingOptions GetImageSamplingOptions(bool interpolate)
    {
        if (interpolate)
        {
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
        }
        return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }
}