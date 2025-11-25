using SkiaSharp;
using System;

namespace PdfReader.Color.Filters;

/// <summary>
/// Provides Skia color filters for PDF decode mapping per channel and for image masks.
/// Supports Grayscale (1 channel), RGB (3 channels), and CMYK (4 channels).
/// Leaves alpha channel untouched except for mask images.
/// </summary>
internal class MatrixColorFilters
{
    /// <summary>
    /// Builds a color matrix filter that applies the PDF /Decode array to the raw image components.
    /// This is applied before any color space conversion, so the channels represent the original color space (e.g., Gray, RGB, CMYK).
    /// </summary>
    /// <param name="decode">The /Decode array, or null for default mapping.</param>
    /// <param name="components">The number of color components (e.g., 1=Gray, 3=RGB, 4=CMYK).</param>
    /// <returns>An SKColorFilter that applies the decode mapping, or null if not needed.</returns>
    public static SKColorFilter BuildDecodeColorMatrix(float[] decode, int components)
    {
        if (decode == null || decode.Length != components * 2)
        {
            return null;
        }

        var colorMatrix = BuildColorMatrix(decode, components);

        return SKColorFilter.CreateColorMatrix(colorMatrix);
    }

    public static float[] BuildColorMatrix(float[] decode, int components)
    {
        if (components != 1 && components != 3 && components != 4)
            throw new ArgumentException("Only 1, 3, and 4 components supported.");
        if (decode.Length != components * 2)
            throw new ArgumentException("Decode array length mismatch.");

        // Compute scale and offset per input component
        var scale = new float[components];
        var offset = new float[components];

        for (int i = 0; i < components; i++)
        {
            float d0 = decode[i * 2];
            float d1 = decode[i * 2 + 1];
            scale[i] = d1 - d0;  // multiply
            offset[i] = d0;       // add
        }

        float[] colorMatrix;

        if (components == 1)
        {
            // 1-channel images: output only to R, leave G,B zero.
            // A stays unchanged.
            //
            // After this, another pipeline stage will map this correctly (e.g. grayscale or CMYK).
            colorMatrix =
            [
                scale[0], 0,        0,        0, offset[0], // R output
                0,        0,        0,        0, 0,         // G = 0
                0,        0,        0,        0, 0,         // B = 0
                0,        0,        0,        1, 0          // A unchanged
            ];
        }
        else if (components == 3)
        {
            // RGB (raw, no color-space interpretation)
            colorMatrix =
            [
                scale[0], 0,        0,        0, offset[0], // R
                0,        scale[1], 0,        0, offset[1], // G
                0,        0,        scale[2], 0, offset[2], // B
                0,        0,        0,        1, 0          // A unchanged
            ];
        }
        else // components == 4
        {
            // RGBA — but channels may contain CMYK in disguise (R=C, G=M, B=Y, A=K)
            // Still, only apply raw decode remap here.
            colorMatrix =
            [
                scale[0], 0,        0,        0,        offset[0], // R (C)
                0,        scale[1], 0,        0,        offset[1], // G (M)
                0,        0,        scale[2], 0,        offset[2], // B (Y)
                0,        0,        0,        scale[3], offset[3]  // A (K)
            ];
        }

        return colorMatrix;
    }

    /// <summary>
    /// Builds a color matrix filter that maps the red channel to the alpha channel.
    /// </summary>
    /// <param name="inverse">If true - inverts the mapping (1 - R channel).</param>
    /// <returns><see cref="SKColorFilter"/> that maps the red channel to the alpha channel.</returns>
    public static SKColorFilter BuildGrayAlphaColorMatrix(bool inverse)
    {
        float[] rToAlphaMatrix;

        if (inverse)
        {
            rToAlphaMatrix =
            [
                0, 0, 0, 0, 0,  // R output: 0
                0, 0, 0, 0, 0,  // G output: 0
                0, 0, 0, 0, 0,  // B output: 0
               -1, 0, 0, 0, 1   // A output: 1 - R channel
            ];
        }
        else
        {
            rToAlphaMatrix =
            [
                0, 0, 0, 0, 0, // R output: 0
                0, 0, 0, 0, 0, // G output: 0
                0, 0, 0, 0, 0, // B output: 0
                1, 0, 0, 0, 0  // A output: R channel
            ];
        }

        return SKColorFilter.CreateColorMatrix(rToAlphaMatrix);
    }
}
