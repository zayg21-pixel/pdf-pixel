using System;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Factory for creating IGrayRowDecoder instances based on grayscale bit depth.
    /// </summary>
    internal static class GrayRowDecoderFactory
    {
        /// <summary>
        /// Creates an IGrayRowDecoder for the specified grayscale row format.
        /// </summary>
        /// <param name="columns">The number of pixels (columns) in the row.</param>
        /// <param name="bitsPerComponent">The number of bits per grayscale component (1, 2, 4, 8, 16).</param>
        /// <param name="pixelProcessor">The pixel processor to apply per-pixel post-processing.</param>
        /// <returns>An IGrayRowDecoder for the specified format.</returns>
        /// <exception cref="ArgumentException">Thrown if the bit depth is not supported.</exception>
        public static IGrayRowDecoder Create(
            int columns,
            int bitsPerComponent,
            PdfPixelProcessor pixelProcessor)
        {
            switch (bitsPerComponent)
            {
                case 1:
                    return new Gray1RowDecoder(columns, pixelProcessor);
                case 2:
                    return new Gray2RowDecoder(columns, pixelProcessor);
                case 4:
                    return new Gray4RowDecoder(columns, pixelProcessor);
                case 8:
                    return new Gray8RowDecoder(columns, pixelProcessor);
                case 16:
                    return new Gray16RowDecoder(columns, pixelProcessor);
                default:
                    throw new ArgumentException($"Unsupported bitsPerComponent for grayscale: {bitsPerComponent}");
            }
        }
    }
}
