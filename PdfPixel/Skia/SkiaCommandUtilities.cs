using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Image;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Skia.Cache;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Skia;

/// <summary>
/// Skia-specific helper methods for commands, e.g. for applying modifiers to paints or resolving typefaces.
/// </summary>
internal static class SkiaCommandUtilities
{
    /// <summary>
    /// Applies the execution context's uncolored paint modifier to <paramref name="paint"/>, if one is set.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ModifyPaint(SKPaint paint, PdfCommandExecutionContext executionContext)
    {
        if (executionContext.UncoloredModifier == null)
        {
            return;
        }

        using SKColorFilter filter = CreateUncoloredColorFilter(executionContext.UncoloredModifier.Color);

        if (paint.ColorFilter != null)
        {
            using SKColorFilter composed = SKColorFilter.CreateCompose(filter, paint.ColorFilter);
            paint.ColorFilter = composed;
        }
        else
        {
            paint.ColorFilter = filter;
        }
    }

    private static SKColorFilter CreateUncoloredColorFilter(in PdfColor color)
    {
        // PDF uncolored semantics (concise):
        // final RGB = paint.rgb * paint.alpha * srcA
        // final A   = paint.alpha * srcA
        float pr = color.Red;
        float pg = color.Green;
        float pb = color.Blue;
        float pa = color.Alpha;

        float rMul = pr * pa;
        float gMul = pg * pa;
        float bMul = pb * pa;

        var matrix = new float[]
        {
            0f,
            0f,
            0f,
            rMul,
            0f,
            0f,
            0f,
            0f,
            gMul,
            0f,
            0f,
            0f,
            0f,
            bMul,
            0f,
            0f,
            0f,
            0f,
            pa,
            0f
        };

        return SKColorFilter.CreateColorMatrix(matrix);
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
    /// Sets font edging based on the antialias flag.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ApplyAntialias(SKFont font, bool antialias) => font.Edging = antialias ? SKFontEdging.SubpixelAntialias : SKFontEdging.Alias;

    /// <summary>
    /// Returns the <see cref="SKTypeface"/> backing <paramref name="typeface"/>, building it from font
    /// bytes only once per document and caching it on <see cref="IPdfDocument.CommandCache"/> so every
    /// command drawing with the same typeface reuses the same native typeface.
    /// </summary>
    public static SKTypeface GetOrCreateSkTypeface(PdfCommandExecutionContext executionContext, IPdfTypeface typeface)
    {
        TypefaceCommandCacheKey key = new(typeface);
        CommandCache cache = executionContext.Document.CommandCache;

        lock (executionContext.ContentLocker)
        {
            if (cache.GetEntry(key) is SkTypefaceCommandCacheEntry existing)
            {
                return existing.Typeface;
            }

            using Stream fontStream = typeface.GetFontStream();
            SKTypeface skTypeface = SKTypeface.FromStream(fontStream) ?? throw new InvalidOperationException("Failed to create typeface from font data.");

            cache.StoreEntry(key, new SkTypefaceCommandCacheEntry(skTypeface));

            return skTypeface;
        }
    }

    /// <summary>
    /// Creates a paint that draws <paramref name="shader"/> with the blend mode and fill
    /// alpha captured in <paramref name="context"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint GetBaseImagePaint(SKShader shader, ImageDecodingContext context)
    {
        return new()
        {
            Shader = shader,
            BlendMode = (context.IsStencilMaskComposite) ? SKBlendMode.DstIn : context.FillPaint.BlendMode.ToSkiaBlendMode(),
            Color = ApplyAlpha(SKColors.White, context.FillPaint.Alpha)
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKPaint GetBaseImagePaint(ImageDecodingContext context)
    {
        return new()
        {
            BlendMode = (context.IsStencilMaskComposite) ? SKBlendMode.DstIn : context.FillPaint.BlendMode.ToSkiaBlendMode(),
            Color = ApplyAlpha(SKColors.White, context.FillPaint.Alpha)
        };
    }

    /// <summary>
    /// Returns the sampling options to draw a tile with, based on whether it should be interpolated.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SKSamplingOptions GetSamplingOptions(bool interpolate)
        => interpolate ? new SKSamplingOptions(SKFilterMode.Linear) : new SKSamplingOptions(SKFilterMode.Nearest);
}
