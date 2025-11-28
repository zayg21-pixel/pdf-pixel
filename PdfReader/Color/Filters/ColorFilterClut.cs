using PdfReader.Color.ColorSpace;
using SkiaSharp;
using System;

namespace PdfReader.Color.Filters;

/// <summary>
/// Controls LUT fidelity for color filter-based color conversion.
/// </summary>
public enum ColorFilterClutResolution
{
    /// <summary>
    /// Low fidelity (16x16x16 grid).
    /// </summary>
    Low,
    /// <summary>
    /// Standard fidelity (32x32x32 grid).
    /// </summary>
    Normal,
    /// <summary>
    /// High fidelity (64x64x64 grid).
    /// </summary>
    High
}

/// <summary>
/// Provides Skia color filter-based color conversion using a 3D LUT.
/// </summary>
internal class ColorFilterClut
{
    // Static cache for runtime effects
    private static readonly SKRuntimeEffect Clut3dEffect;
    private static readonly SKRuntimeEffect ClutKSliceEffect;
    private static readonly SKRuntimeEffect IndexedEffect;
    
    static ColorFilterClut()
    {
        string shaderSource3D = @"
                uniform shader lut;
                uniform float lutSize;

                half4 main(half4 inputColor) {
                    half3 lutPosition = inputColor.rgb * (lutSize - 1.0);

                    half redIndex = lutPosition.r;
                    half greenIndex = lutPosition.g;
                    half blueIndex = lutPosition.b;

                    half blueIndexLower = floor(blueIndex);
                    half blueIndexUpper = min(blueIndexLower + 1.0, lutSize - 1.0);
                    half blueFraction = blueIndex - blueIndexLower;

                    half2 uvLower = half2(redIndex + 0.5, blueIndexLower * lutSize + greenIndex + 0.5);
                    half2 uvUpper = half2(redIndex + 0.5, blueIndexUpper * lutSize + greenIndex + 0.5);

                    half4 colorLower = lut.eval(uvLower);
                    half4 colorUpper = lut.eval(uvUpper);

                    half4 interpolatedColor = mix(colorLower, colorUpper, blueFraction);
                    return half4(interpolatedColor.rgb, inputColor.a);
                }
            ";
        Clut3dEffect = SKRuntimeEffect.CreateColorFilter(shaderSource3D, out var error3D);
        if (Clut3dEffect == null)
        {
            throw new InvalidOperationException($"Failed to compile 3D CLUT shader: {error3D}");
        }

        string shaderSourceKSlice = @"
                uniform shader lut;
                uniform float lutSize;
                uniform float kSliceCount;

                half4 main(half4 cmykColor) {
                    half3 cmyColor = saturate(cmykColor.bgr);
                    half blackChannel = saturate(cmykColor.a);

                    // Convert to LUT index space
                    half3 lutPosition = cmyColor * (lutSize - 1.0);

                    // Interpolation along the yellow axis
                    half yellowFraction = fract(lutPosition.z);
                    half yellowIndexLower = floor(lutPosition.z);
                    half yellowIndexUpper = min(yellowIndexLower + 1.0, lutSize - 1.0);

                    // Interpolation along the black axis
                    half blackPosition = blackChannel * (kSliceCount - 1.0);
                    half blackIndexLower = floor(blackPosition);
                    half blackIndexUpper = min(blackIndexLower + 1.0, kSliceCount - 1.0);
                    half blackFraction = fract(blackPosition);

                    // Compute UV coordinates for LUT sampling
                    half2 uvCmyk00 = half2(lutPosition.x + 0.5, blackIndexLower * lutSize * lutSize + yellowIndexLower * lutSize + lutPosition.y + 0.5);
                    half2 uvCmyk01 = half2(lutPosition.x + 0.5, blackIndexLower * lutSize * lutSize + yellowIndexUpper * lutSize + lutPosition.y + 0.5);
                    half2 uvCmyk10 = half2(lutPosition.x + 0.5, blackIndexUpper * lutSize * lutSize + yellowIndexLower * lutSize + lutPosition.y + 0.5);
                    half2 uvCmyk11 = half2(lutPosition.x + 0.5, blackIndexUpper * lutSize * lutSize + yellowIndexUpper * lutSize + lutPosition.y + 0.5);

                    half4 colorK0Lower = lut.eval(uvCmyk00);
                    half4 colorK0Upper = lut.eval(uvCmyk01);
                    half4 interpolatedK0 = mix(colorK0Lower, colorK0Upper, yellowFraction);

                    half4 colorK1Lower = lut.eval(uvCmyk10);
                    half4 colorK1Upper = lut.eval(uvCmyk11);
                    half4 interpolatedK1 = mix(colorK1Lower, colorK1Upper, yellowFraction);

                    // Blend between black slices
                    return mix(interpolatedK0, interpolatedK1, blackFraction);
                }
            ";
        ClutKSliceEffect = SKRuntimeEffect.CreateColorFilter(shaderSourceKSlice, out var errorKSlice);
        if (ClutKSliceEffect == null)
        {
            throw new InvalidOperationException($"Failed to compile CMYK CLUT shader: {errorKSlice}");
        }

        string shaderSourceIndexed = @"
                uniform shader palette;
                uniform float paletteSize;
                half4 main(half4 inColor) {
                    float idx = clamp(inColor.r * 255.0, 0.0, paletteSize - 1.0);
                    float texCoord = (idx + 0.5);
                    return palette.eval(float2(texCoord, 0.5));
                }
            ";
        IndexedEffect = SKRuntimeEffect.CreateColorFilter(shaderSourceIndexed, out var errorIndexed);
        if (IndexedEffect == null)
        {
            throw new InvalidOperationException($"Failed to compile indexed palette shader: {errorIndexed}");
        }
    }

    /// <summary>
    /// Returns the grid size for the specified ColorFilterClutResolution.
    /// </summary>
    /// <param name="resolution">The LUT resolution.</param>
    /// <returns>The grid size for the LUT.</returns>
    private static int GetGridSizeForResolution(ColorFilterClutResolution resolution)
    {
        switch (resolution)
        {
            case ColorFilterClutResolution.Low:
                return 16;
            case ColorFilterClutResolution.Normal:
                return 32;
            case ColorFilterClutResolution.High:
                return 64;
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unknown ColorFilterClutResolution value.");
        }
    }

    /// <summary>
    /// Builds a true CLUT color filter for the specified number of channels (1, 3, or 4).
    /// For 1 or 3 channels, builds a 3D LUT color filter. For 4 channels, builds a k-sliced (4D) LUT color filter.
    /// </summary>
    /// <param name="resolution">The LUT resolution.</param>
    /// <param name="channelCount">Number of channels (1, 3, or 4).</param>
    /// <param name="renderingIntent">PDF rendering intent for color conversion.</param>
    /// <param name="deviceToSrgbConverter">Device-to-sRGB conversion delegate.</param>
    /// <returns>SKColorFilter for the specified channel configuration.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if channelCount is not 1, 3, or 4.</exception>
    public static SKColorFilter BuildClutColorFilter(
        ColorFilterClutResolution resolution,
        int channelCount,
        PdfRenderingIntent renderingIntent,
        DeviceToSrgbCore deviceToSrgbConverter)
    {
        if (channelCount == 4)
        {
            return BuildClutColorFilterKSlice(resolution, renderingIntent, deviceToSrgbConverter);
        }
        else
        {
            return BuildClutColorFilter3D(resolution, renderingIntent, deviceToSrgbConverter);
        }
    }

    /// <summary>
    /// Builds a true CLUT color filter for 3D LUTs (1 or 3 channels) using Skia RuntimeEffect.
    /// </summary>
    private static SKColorFilter BuildClutColorFilter3D(
        ColorFilterClutResolution resolution,
        PdfRenderingIntent renderingIntent,
        DeviceToSrgbCore deviceToSrgbConverter)
    {
        int lutAxisSize = GetGridSizeForResolution(resolution);
        using SKBitmap lutBitmap = ColorFilterTexture.BuildLutBitmap(lutAxisSize, renderingIntent, deviceToSrgbConverter);

        var uniforms = new SKRuntimeEffectUniforms(Clut3dEffect)
        {
            ["lutSize"] = (float)lutAxisSize
        };

        SKRuntimeEffectChildren children = new SKRuntimeEffectChildren(Clut3dEffect)
        {
            { "lut", ColorFilterTexture.ToLutShader(lutBitmap) }
        };

        return Clut3dEffect.ToColorFilter(uniforms, children);
    }

    /// <summary>
    /// Builds an <see cref="SKColorFilter"/> for indexed color spaces using a palette texture and a runtime shader.
    /// </summary>
    /// <param name="palette">The palette of <see cref="SKColor"/> values representing the indexed color table.</param>
    /// <returns>
    /// An <see cref="SKColorFilter"/> that maps grayscale input (where the R channel is the palette index in [0,1])
    /// to the corresponding color from the palette. The filter uses a shader to perform the lookup efficiently.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="palette"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the shader cannot be compiled.</exception>
    public static SKColorFilter BuildIndexedColorFilter(SKColor[] palette)
    {
        if (palette == null)
        {
            throw new ArgumentNullException(nameof(palette));
        }

        int paletteSize = palette.Length;

        using SKBitmap paletteBitmap = new SKBitmap(paletteSize, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        paletteBitmap.Pixels = palette;

        var uniforms = new SKRuntimeEffectUniforms(IndexedEffect)
        {
            ["paletteSize"] = (float)paletteSize
        };

        var children = new SKRuntimeEffectChildren(IndexedEffect)
        {
            { "palette", ColorFilterTexture.ToLutShader(paletteBitmap) }
        };

        return IndexedEffect.ToColorFilter(uniforms, children);
    }

    /// <summary>
    /// Returns the recommended number of K slices for the specified ColorFilterClutResolution.
    /// </summary>
    /// <param name="resolution">The LUT resolution.</param>
    /// <returns>The recommended number of K slices for CMYK LUTs.</returns>
    private static int GetKSlicesForResolution(ColorFilterClutResolution resolution)
    {
        switch (resolution)
        {
            case ColorFilterClutResolution.Low:
                return 8;
            case ColorFilterClutResolution.Normal:
                return 16;
            case ColorFilterClutResolution.High:
                return 32;
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Unknown ColorFilterClutResolution value.");
        }
    }

    /// <summary>
    /// Builds a CLUT color filter for CMYK input using Skia RuntimeEffect.
    /// The LUT is organized as an N x NN grid with kSlices layers (K axis).
    /// Bilinear interpolation is performed in the CMY axes using EvaluateCmy, then the two results are blended on the K axis.
    /// </summary>
    private static SKColorFilter BuildClutColorFilterKSlice(
        ColorFilterClutResolution resolution,
        PdfRenderingIntent renderingIntent,
        DeviceToSrgbCore deviceToSrgbConverter)
    {
        int gridSize = GetGridSizeForResolution(resolution);
        int kSlices = GetKSlicesForResolution(resolution);
        using SKBitmap lutBitmap = ColorFilterTexture.BuildKSliceLutBitmap(gridSize, kSlices, renderingIntent, deviceToSrgbConverter);

        var uniforms = new SKRuntimeEffectUniforms(ClutKSliceEffect)
        {
            ["lutSize"] = (float)gridSize,
            ["kSliceCount"] = (float)kSlices
        };

        SKRuntimeEffectChildren children = new SKRuntimeEffectChildren(ClutKSliceEffect)
        {
            { "lut", ColorFilterTexture.ToLutShader(lutBitmap) }
        };

        return ClutKSliceEffect.ToColorFilter(uniforms, children);
    }
}
