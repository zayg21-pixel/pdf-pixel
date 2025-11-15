using PdfReader.Imaging.Sampling.Cmyk;
using PdfReader.Imaging.Sampling.Rgb;
using System;

namespace PdfReader.Imaging.Sampling;

/// <summary>
/// Factory for creating upsampler instances based on image component and bit depth.
/// </summary>
internal static class RgbaRowUpsamplerFactory
{
    /// <summary>
    /// Creates an upsampler for the specified image row format.
    /// </summary>
    /// <param name="columns">The number of pixels (columns) in the row.</param>
    /// <param name="components">The number of color components per pixel (1=Gray, 3=RGB, 4=CMYK).</param>
    /// <param name="bitsPerComponent">The number of bits per color component (1, 2, 4, 8, 16).</param>
    /// <exception cref="ArgumentException">Thrown if the combination is not supported.</exception>
    public static IRowUpsampler Create(
        int columns,
        int components,
        int bitsPerComponent)
    {
        return components switch
        {
            1 => bitsPerComponent switch
            {
                1 => new GrayRgba1RowUpsampler(columns),
                2 => new GrayRgba2RowUpsampler(columns),
                4 => new GrayRgba4RowUpsampler(columns),
                8 => new GrayRgba8RowUpsampler(columns),
                16 => new GrayRgba16RowUpsampler(columns),
                _ => throw new ArgumentException($"Unsupported bitsPerComponent for RGB: {bitsPerComponent}"),
            },
            3 => bitsPerComponent switch
            {
                1 => new Rgb1RowUpsampler(columns),
                2 => new Rgb2RowUpsampler(columns),
                4 => new Rgb4RowUpsampler(columns),
                8 => new Rgb8RowUpsampler(columns),
                16 => new Rgb16RowUpsampler(columns),
                _ => throw new ArgumentException($"Unsupported bitsPerComponent for RGB: {bitsPerComponent}"),
            },
            4 => bitsPerComponent switch
            {
                1 => new Cmyk1RowUpsampler(columns),
                2 => new Cmyk2RowUpsampler(columns),
                4 => new Cmyk4RowUpsampler(columns),
                8 => new Cmyk8RowUpsampler(columns),
                16 => new Cmyk16RowUpsampler(columns),
                _ => throw new ArgumentException($"Unsupported bitsPerComponent for CMYK: {bitsPerComponent}"),
            },
            _ => throw new ArgumentException($"Unsupported component count: {components}"),
        };
    }
}
