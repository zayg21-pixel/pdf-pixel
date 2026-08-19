using SkiaSharp;

namespace PdfPixel.Skia;

/// <summary>
/// Provides SKSL-based stencil mask painting with optional alpha inversion.
/// </summary>
internal static class SkiaImageBlending
{
    private static readonly SKRuntimeEffect _imageMaskColorFilterEffect;

    static SkiaImageBlending()
    {
        const string imageMaskColorFilterSksl = @"
                uniform half3 fillColor;
                uniform half useInverse;

                half4 main(half4 color) {
                    half gray = color.r;
                    half maskAlpha = mix(gray, 1.0 - gray, useInverse);
                    return half4(fillColor * maskAlpha, maskAlpha);
                }
            ";

        _imageMaskColorFilterEffect = SKRuntimeEffect.CreateColorFilter(imageMaskColorFilterSksl, out _);
    }

    /// <summary>
    /// Creates a color filter that turns a grayscale stencil mask image into the fill color,
    /// using the mask's own gray value as alpha, with optional inversion. Applied directly to
    /// the image being drawn, so the effect stays within the image's bounds.
    /// </summary>
    public static SKColorFilter CreateImageMaskColorFilter(in SKColor fillColor, bool inverse)
    {
        SKRuntimeEffectUniforms uniforms = new(_imageMaskColorFilterEffect)
        {
            ["fillColor"] = fillColor,
            ["useInverse"] = inverse ? 1.0f : 0.0f
        };

        return _imageMaskColorFilterEffect.ToColorFilter(uniforms);
    }
}
