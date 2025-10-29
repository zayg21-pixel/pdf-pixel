using System;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Factory for creating IRgbaRowDecoder instances based on image component and bit depth.
    /// </summary>
    internal static class RgbaRowDecoderFactory
    {
        /// <summary>
        /// Creates an IRgbaRowDecoder for the specified image row format.
        /// </summary>
        /// <param name="columns">The number of pixels (columns) in the row.</param>
        /// <param name="components">The number of color components per pixel (1=Gray, 3=RGB, 4=CMYK).</param>
        /// <param name="bitsPerComponent">The number of bits per color component (1, 2, 4, 8, 16).</param>
        /// <exception cref="ArgumentException">Thrown if the combination is not supported.</exception>
        public static IRgbaRowDecoder Create(
            int columns,
            int components,
            int bitsPerComponent)
        {
            switch (components)
            {
                case 1:
                    switch (bitsPerComponent)
                    {
                        case 1: return new GrayRgba1RowDecoder(columns);
                        case 2: return new GrayRgba2RowDecoder(columns);
                        case 4: return new GrayRgba4RowDecoder(columns);
                        case 8: return new GrayRgba8RowDecoder(columns);
                        case 16: return new GrayRgba16RowDecoder(columns);
                        default:
                            throw new ArgumentException($"Unsupported bitsPerComponent for RGB: {bitsPerComponent}");
                    }
                case 3:
                    switch (bitsPerComponent)
                    {
                        case 1: return new Rgb1RowDecoder(columns);
                        case 2: return new Rgb2RowDecoder(columns);
                        case 4: return new Rgb4RowDecoder(columns);
                        case 8: return new Rgb8RowDecoder(columns);
                        case 16: return new Rgb16RowDecoder(columns);
                        default:
                            throw new ArgumentException($"Unsupported bitsPerComponent for RGB: {bitsPerComponent}");
                    }
                case 4:
                    switch (bitsPerComponent)
                    {
                        case 1: return new Cmyk1RowDecoder(columns);
                        case 2: return new Cmyk2RowDecoder(columns);
                        case 4: return new Cmyk4RowDecoder(columns);
                        case 8: return new Cmyk8RowDecoder(columns);
                        case 16: return new Cmyk16RowDecoder(columns);
                        default:
                            throw new ArgumentException($"Unsupported bitsPerComponent for CMYK: {bitsPerComponent}");
                    }
                default:
                    throw new ArgumentException($"Unsupported component count: {components}");
            }
        }
    }
}
