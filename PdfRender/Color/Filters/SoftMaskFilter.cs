using SkiaSharp;
using System;

namespace PdfRender.Color.Filters;

/// <summary>
/// Provides Skia color filter-based alpha masking using a mask array and PDF dematting algorithm.
/// </summary>
internal static class SoftMaskFilter
{
    // Static cache for runtime effects
    private static readonly SKRuntimeEffect MaskEffect;
    private static readonly SKRuntimeEffect DematteEffect;

    static SoftMaskFilter()
    {
        string shaderSourceMask = @"
                uniform shader maskLut;
                half4 main(half4 inColor) {
                    float rTexCoord = clamp(inColor.r * 255.0, 0.0, 255.0) + 0.5;
                    float gTexCoord = clamp(inColor.g * 255.0, 0.0, 255.0) + 0.5;
                    float bTexCoord = clamp(inColor.b * 255.0, 0.0, 255.0) + 0.5;
                    half rMask = maskLut.eval(float2(rTexCoord, 0.5)).a;
                    half gMask = maskLut.eval(float2(gTexCoord, 1.5)).a;
                    half bMask = maskLut.eval(float2(bTexCoord, 2.5)).a;
                    half alpha = (rMask == 0.0 && gMask == 0.0 && bMask == 0.0) ? 0.0 : 1.0;
                    return half4(inColor.rgb, alpha);
                }
            ";
        MaskEffect = SKRuntimeEffect.CreateColorFilter(shaderSourceMask, out var errorMask);
        if (MaskEffect == null)
        {
            throw new InvalidOperationException($"Failed to compile SoftMaskFilter shader: {errorMask}");
        }

        string shaderSourceDematte = @"
                uniform half3 matte;
                half4 main(half4 color) {
                    float alpha = color.a * 255.0;
                    half3 result;
                    if (alpha == 0.0) {
                        result = matte;
                    } else {
                        result = matte + (color.rgb * 255.0 - matte) * 255.0 / alpha;
                    }
                    result = clamp(result, 0.0, 255.0) / 255.0;
                    return half4(result, 1.0);
                }
            ";
        DematteEffect = SKRuntimeEffect.CreateColorFilter(shaderSourceDematte, out var errorDematte);
        if (DematteEffect == null)
        {
            throw new InvalidOperationException($"Failed to compile DematteColorFilter shader: {errorDematte}");
        }
    }

    /// <summary>
    /// Builds an <see cref="SKColorFilter"/> that applies an alpha mask based on the provided mask ranges for grayscale or RGB images.
    /// The filter maps each component to alpha values according to the PDF /Mask specification.
    /// </summary>
    /// <param name="maskRanges">The mask array, where each consecutive pair [min, max] defines a range of sample values to be made transparent for each component. Supports grayscale (2 values) and RGB (6 values). Returns null for other cases.</param>
    /// <param name="upsample">If true - mask value will be up-sampled to match normalized color components.</param>
    /// <param name="bitsPerComponent">The number of bits per component in the image. Used to normalize mask and pixel values to 0–255.</param>
    /// <returns>
    /// An <see cref="SKColorFilter"/> that sets alpha to 0.0 if all input components are in their mask range, otherwise 1.0. Returns null if maskRanges is not for grayscale or RGB.
    /// </returns>
    public static SKColorFilter BuildMaskColorFilter(int[] maskRanges, bool upsample, int bitsPerComponent)
    {
        if (maskRanges == null || maskRanges.Length == 0)
        {
            return null;
        }

        if (bitsPerComponent < 1 || bitsPerComponent > 16)
        {
            // Arbitrary upper bound for sanity; PDF spec allows up to 16
            return null;
        }

        if (!upsample && bitsPerComponent >= 16)
        {
            // Only supporting 8 and less when not upsampling
            return null;
        }

        int maxSampleValue = (1 << bitsPerComponent) - 1;

        // Use 1D array: [R0..R255, G0..G255, B0..B255]
        byte[] maskLut = new byte[256 * 3];
        if (maskRanges.Length == 2)
        {
            int min = maskRanges[0];
            int max = maskRanges[1];
            for (int value = 0; value < 256; value++)
            {
                int sampleValue = upsample ? (int)Math.Round((double)value * maxSampleValue / 255.0) : value;
                byte maskValue = (byte)(sampleValue >= min && sampleValue <= max ? 0 : 255);
                maskLut[value] = maskValue; // R
                maskLut[256 + value] = maskValue; // G
                maskLut[512 + value] = maskValue; // B
            }
        }
        else if (maskRanges.Length == 6)
        {
            for (int component = 0; component < 3; component++)
            {
                int min = maskRanges[component * 2];
                int max = maskRanges[component * 2 + 1];
                for (int value = 0; value < 256; value++)
                {
                    int sampleValue = upsample ? (int)Math.Round((double)value * maxSampleValue / 255.0) : value;
                    maskLut[component * 256 + value] = (byte)(sampleValue >= min && sampleValue <= max ? 0 : 255);
                }
            }
        }
        else
        {
            // Unsupported mask (e.g., CMYK): return null
            return null;
        }

        using var maskBitmap = new SKBitmap(256, 3, SKColorType.Alpha8, SKAlphaType.Premul);
        System.Runtime.InteropServices.Marshal.Copy(maskLut, 0, maskBitmap.GetPixels(), maskLut.Length);

        var uniforms = new SKRuntimeEffectUniforms(MaskEffect);
        var children = new SKRuntimeEffectChildren(MaskEffect)
        {
            { "maskLut", maskBitmap.ToShader() }
        };

        return MaskEffect.ToColorFilter(uniforms, children);
    }

    /// <summary>
    /// Builds an <see cref="SKColorFilter"/> that applies PDF 2.0 dematting algorithm (§11.9.6) using the specified matte color.
    /// </summary>
    /// <param name="matte">The matte <see cref="SKColor"/> to use for dematting.</param>
    /// <returns>
    /// An <see cref="SKColorFilter"/> that removes the matte effect from each pixel according to the PDF spec.
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the shader cannot be compiled.</exception>
    public static SKColorFilter BuildDematteColorFilter(SKColor matte)
    {
        var uniforms = new SKRuntimeEffectUniforms(DematteEffect)
        {
            ["matte"] = new SKColor(matte.Red, matte.Green, matte.Blue, 255)
        };

        return DematteEffect.ToColorFilter(uniforms);
    }
}
