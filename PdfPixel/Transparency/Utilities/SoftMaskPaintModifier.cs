using System;
using PdfPixel.Commands;
using PdfPixel.Transparency.Model;
using SkiaSharp;

namespace PdfPixel.Transparency.Utilities;

/// <summary>
/// Command modifier that applies DstIn blending and an optional luminosity-to-alpha
/// color filter to every paint during replay of recorded soft mask commands.
/// </summary>
internal sealed class SoftMaskPaintModifier : IPdfCommandModifier
{
    private static readonly SKRuntimeEffect _maskBlenderEffect;
    private static readonly SKBlender _maskBlender;

    static SoftMaskPaintModifier()
    {
        // Blender entry point: half4 main(half4 src, half4 dst)
        //   src = mask content pixel (alpha encoded as luminance / R channel)
        //   dst = pixel already on the canvas
        // Extracts src luminance and multiplies dst by it (DstIn via luma).
        const string blenderSksl = @"
            half4 main(half4 src, half4 dst)
            {
                return half4(dst.rgb, src.r * dst.a);
            }
        ";

        _maskBlenderEffect = SKRuntimeEffect.CreateBlender(blenderSksl, out string errors);

        if (_maskBlenderEffect == null)
        {
            throw new InvalidOperationException(
                $"Failed to compile soft-mask blender SkSL: {errors}");
        }

        var uniforms = new SKRuntimeEffectUniforms(_maskBlenderEffect);
        _maskBlender = _maskBlenderEffect.ToBlender(uniforms);
    }

    /// <summary>
    /// Creates a modifier for soft mask replay.
    /// Both <see cref="PdfSoftMaskSubtype.Alpha"/> and <see cref="PdfSoftMaskSubtype.Luminosity"/>
    /// are handled by the same SkSL blender, which reads the source luminance (R channel)
    /// and uses it to modulate the destination.
    /// </summary>
    /// <param name="subtype">The soft mask subtype (Alpha or Luminosity).</param>
    public SoftMaskPaintModifier(PdfSoftMaskSubtype subtype)
    {
    }

    /// <summary>
    /// Replaces the paint's blend mode with a custom SkSL blender that extracts
    /// the source pixel's luminance and multiplies it into the destination.
    /// This performs a luminance-driven DstIn in a single blender pass.
    /// </summary>
    public void ModifyPaint(SKPaint paint)
    {
        paint.Blender = _maskBlender;
    }

    public void Dispose()
    {
        // Static blender and effect are intentionally kept alive for the process lifetime.
    }
}
