using PdfReader.Color.ColorSpace;
using PdfReader.Imaging.Jpg.Color;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
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
        if (PdfImageRowProcessor.ShouldConvertColor(image))
        {
            return null;
        }

        // those cases requires palette building, but JPG does not support palette
        if (image.ColorSpaceConverter.Components == 1 && !(image.ColorSpaceConverter is DeviceGrayConverter || image.ColorSpaceConverter is IccBasedConverter))
        {
            return null;
        }

        var data = image.GetImageData();

        if (image.ColorSpaceConverter is IccBasedConverter iccBased)
        {
            data = JpgIccProfileUpdater.UpdateIccProfile(data, iccBased.Profile?.Bytes);
        }

        return SKImage.FromEncodedData(data.Span);
    }
}
