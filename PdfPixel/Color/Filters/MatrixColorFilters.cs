using SkiaSharp;

namespace PdfPixel.Color.Filters;

/// <summary>
/// Provides Skia color filters for PDF decode mapping per channel and for image masks.
/// Supports Grayscale (1 channel), RGB (3 channels), and CMYK (4 channels).
/// Leaves alpha channel untouched except for mask images.
/// </summary>
internal class MatrixColorFilters
{
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
