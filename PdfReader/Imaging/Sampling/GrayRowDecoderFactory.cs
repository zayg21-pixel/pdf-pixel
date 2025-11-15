using PdfReader.Imaging.Sampling.Gray;
using System;

namespace PdfReader.Imaging.Sampling;

/// <summary>
/// Factory for creating upsampler instances based on grayscale bit depth.
/// </summary>
internal static class GrayRowUpsamplerFactory
{
    /// <summary>
    /// Creates an upsampler for the specified grayscale row format.
    /// </summary>
    /// <param name="columns">The number of pixels (columns) in the row.</param>
    /// <param name="bitsPerComponent">The number of bits per grayscale component (1, 2, 4, 8, 16).</param>
    /// <param name="upscale">Whether to upscale grayscale values to 8-bit range.</param>
    /// <returns>An upsampler for the specified format.</returns>
    /// <exception cref="ArgumentException">Thrown if the bit depth is not supported.</exception>
    public static IRowUpsampler Create(
        int columns,
        int bitsPerComponent,
        bool upscale)
    {
        return bitsPerComponent switch
        {
            1 => new Gray1RowUpsampler(columns, upscale),
            2 => new Gray2RowUpsampler(columns, upscale),
            4 => new Gray4RowUpsampler(columns, upscale),
            8 => new Gray8RowUpsampler(columns),
            16 => new Gray16RowUpsampler(columns),
            _ => throw new ArgumentException($"Unsupported bitsPerComponent for grayscale: {bitsPerComponent}"),
        };
    }
}
