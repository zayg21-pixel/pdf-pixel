using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;

namespace PdfReader.Imaging.Skia;

/// <summary>
/// Provides helper methods for processing images using SkiaSharp.
/// </summary>
internal class SkiaImageHelpers
{
    /// <summary>
    /// True if skia can be used to process this image directly without color conversion.
    /// </summary>
    /// <param name="image">Image instance.</param>
    /// <returns>True if Skia can be used.</returns>
    public static bool CanUseSkiaFastPath(PdfImage image)
    {
        if (PdfImageRowProcessor.ShouldConvertColor(image))
        {
            return false;
        }

        if (image.ColorSpaceConverter.Components == 1)
        {
            // Skia does not support 16-bit gray images
            return image.BitsPerComponent == 8;
        }

        if (image.ColorSpaceConverter.Components == 3)
        {
            return image.BitsPerComponent == 8 || image.BitsPerComponent == 16;
        }

        return false;
    }
}
