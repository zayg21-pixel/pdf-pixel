using PdfReader.Imaging.Model;
using SkiaSharp;

namespace PdfReader.Imaging.Skia;

internal static class JpgSkiaDecoder
{
    /// <summary>
    /// Uses Skia to decode a JPG image from a PDF image object.
    /// </summary>
    /// <param name="image">Image instance.</param>
    /// <returns>Decoded image.</returns>
    public static SKImage DecodeAsJpg(PdfImage image)
    {
        if (!SkiaImageHelpers.CanUseSkiaFastPath(image))
        {
            return null;
        }

        var stream = image.GetImageDataStream();

        return SKImage.FromEncodedData(stream);
    }
}
