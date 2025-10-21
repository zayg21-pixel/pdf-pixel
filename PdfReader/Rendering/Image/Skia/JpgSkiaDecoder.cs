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

            return SKImage.FromEncodedData(stream);
        }
    }
}
