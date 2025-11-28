using PdfReader.Color.ColorSpace;
using SkiaSharp;
using System;

namespace PdfReader.Color.Filters;

/// <summary>
/// Provides LUT texture generation for color filter-based color conversion.
/// </summary>
internal static class ColorFilterTexture
{
    /// <summary>
    /// Builds a 2D LUT bitmap for Skia color filter sampling.
    /// The LUT is organized as an N x NN grid, where:
    ///   - The X axis (width) represents the red channel index.
    ///   - The Y axis (height) is a tiling of NN rows, each corresponding to a unique (blue, green) combination.
    ///     Specifically, for each blue index, there are N consecutive rows for all green indices.
    /// Each pixel encodes the sRGB color for the corresponding normalized device color (red, green, blue).
    /// Uses direct pointer access for performance.
    /// The layout is code-invariant and does not depend on specific channel order.
    /// </summary>
    public static unsafe SKBitmap BuildLutBitmap(
        int gridSize,
        PdfRenderingIntent renderingIntent,
        DeviceToSrgbCore deviceToSrgbConverter)
    {
        int lutAxisSize = gridSize;
        var lutImageInfo = new SKImageInfo(
            width: lutAxisSize,
            height: lutAxisSize * lutAxisSize,
            colorType: SKColorType.Bgra8888,
            alphaType: SKAlphaType.Premul);

        var lutBitmap = new SKBitmap(lutImageInfo);
        uint* lutPixelPointer = (uint*)lutBitmap.GetPixels();
        float normalizationFactor = 1f / (lutAxisSize - 1);
        Span<float> normalizedRgb = stackalloc float[3];

        for (int blueIndex = 0; blueIndex < lutAxisSize; blueIndex++)
        {
            normalizedRgb[2] = blueIndex * normalizationFactor;
            for (int greenIndex = 0; greenIndex < lutAxisSize; greenIndex++)
            {
                normalizedRgb[1] = greenIndex * normalizationFactor;
                for (int redIndex = 0; redIndex < lutAxisSize; redIndex++)
                {
                    normalizedRgb[0] = redIndex * normalizationFactor;

                    SKColor srgbColor = deviceToSrgbConverter(normalizedRgb, renderingIntent);

                    uint packedColor =
                        (uint)srgbColor.Alpha << 24 |
                        (uint)srgbColor.Blue << 16 |
                        (uint)srgbColor.Green << 8 |
                        srgbColor.Red;

                    int lutPixelIndex = (blueIndex * lutAxisSize + greenIndex) * lutAxisSize + redIndex;
                    lutPixelPointer[lutPixelIndex] = packedColor;
                }
            }
        }

        return lutBitmap;
    }

    /// <summary>
    /// Builds a k-sliced LUT bitmap for CMYK input.
    /// The LUT is organized as an N x NN grid with kSlices layers (K axis).
    /// Uses direct pointer access for performance, matching BuildLutBitmap.
    /// </summary>
    /// <param name="gridSize">The grid size for cyan, magenta, yellow axes.</param>
    /// <param name="sliceCount">The number of slices for the black axis.</param>
    /// <param name="renderingIntent">PDF rendering intent for color conversion.</param>
    /// <param name="deviceToSrgbConverter">Device-to-sRGB conversion delegate for CMYK input.</param>
    /// <returns>SKBitmap representing the k-sliced LUT.</returns>
    public static unsafe SKBitmap BuildKSliceLutBitmap(
        int gridSize,
        int sliceCount,
        PdfRenderingIntent renderingIntent,
        DeviceToSrgbCore deviceToSrgbConverter)
    {
        int lutAxisSize = gridSize;
        int lutImageHeight = lutAxisSize * lutAxisSize * sliceCount;
        var lutImageInfo = new SKImageInfo(
            width: lutAxisSize,
            height: lutImageHeight,
            colorType: SKColorType.Bgra8888,
            alphaType: SKAlphaType.Premul);

        var lutBitmap = new SKBitmap(lutImageInfo);
        uint* lutPixelPointer = (uint*)lutBitmap.GetPixels();
        Span<float> cmykColor = stackalloc float[4];
        float channelNormalization = 1f / (lutAxisSize - 1);
        float sliceNormalization = 1f / (sliceCount - 1);

        for (int blackSliceIndex = 0; blackSliceIndex < sliceCount; blackSliceIndex++)
        {
            cmykColor[3] = blackSliceIndex * sliceNormalization; // Black channel
            int sliceRowOffset = blackSliceIndex * lutAxisSize * lutAxisSize;

            for (int yellowIndex = 0; yellowIndex < lutAxisSize; yellowIndex++)
            {
                cmykColor[2] = yellowIndex * channelNormalization;
                for (int magentaIndex = 0; magentaIndex < lutAxisSize; magentaIndex++)
                {
                    cmykColor[1] = magentaIndex * channelNormalization;
                    int lutRowIndex = sliceRowOffset + yellowIndex * lutAxisSize + magentaIndex;
                    for (int cyanIndex = 0; cyanIndex < lutAxisSize; cyanIndex++)
                    {
                        cmykColor[0] = cyanIndex * channelNormalization;

                        SKColor srgbColor = deviceToSrgbConverter(cmykColor, renderingIntent);

                        uint packedColor =
                            (uint)srgbColor.Alpha << 24 |
                            (uint)srgbColor.Blue << 16 |
                            (uint)srgbColor.Green << 8 |
                            srgbColor.Red;

                        int lutPixelIndex = lutRowIndex * lutAxisSize + cyanIndex;
                        lutPixelPointer[lutPixelIndex] = packedColor;
                    }
                }
            }
        }

        return lutBitmap;
    }

    /// <summary>
    /// Creates a Skia shader for LUT sampling using the specified bitmap.
    /// The shader uses clamp tile mode and linear sampling for best interpolation quality.
    /// </summary>
    /// <param name="lutBitmap">The LUT bitmap to use for shader sampling.</param>
    /// <returns>An SKShader configured for LUT sampling.</returns>
    public static SKShader ToLutShader(SKBitmap lutBitmap)
    {
        if (lutBitmap == null)
        {
            throw new ArgumentNullException(nameof(lutBitmap));
        }

        return lutBitmap.ToShader(
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
    }
}
