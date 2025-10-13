using PdfReader.Rendering.Color;
using SkiaSharp;

namespace PdfReader.Rendering.Image.Skia
{
    internal static class JpgSkiaDecoder
    {
        public static SKImage DecodeAsJpg(PdfImage image)
        {
            if (!SkiaImageHelpers.CanUseSkiaFastPath(image))
            {
                return null;
            }

            var stream = image.GetImageDataStream();

            if (image.ColorSpaceConverter is IccBasedConverter iccConnverter)
            {
                return SkiaImageHelpers.DecodeWithSkiaUsingIcc(stream, iccConnverter);
            }
            else
            {
                return SKImage.FromEncodedData(stream);
            }
        }
    }
}
