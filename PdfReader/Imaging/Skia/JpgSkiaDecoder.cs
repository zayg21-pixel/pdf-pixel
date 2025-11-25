using PdfReader.Color.ColorSpace;
using PdfReader.Color.Filters;
using PdfReader.Imaging.Decoding;
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
    public static PdfImageDecodingResult DecodeAsJpg(PdfImage image)
    {
        if (PdfImageRowProcessor.ShouldConvertColor(image) || (image.ColorSpaceConverter.Components != 1 && image.ColorSpaceConverter.Components != 3))
        {
            return null;
        }

        var data = image.GetImageData();

        bool canApplyColorSpace = image.DecodeArray == null && image.MaskArray == null;
        bool colorConverted = false;

        if (canApplyColorSpace && image.ColorSpaceConverter is IccBasedConverter iccBased)
        {
            data = JpgIccProfileUpdater.UpdateIccProfile(data, iccBased.Profile?.Bytes);
            colorConverted = true;
        }

        var result = SKImage.FromEncodedData(data.Span);

        if (result == null)
        {
            return null;
        }

        return new PdfImageDecodingResult(result)
        {
            DecodeApplied = colorConverted,
            MaskRemoved = colorConverted,
            ColorConverted = colorConverted
        };
    }
}
