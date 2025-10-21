namespace PdfReader.Rendering.Image.Skia
{
    internal class SkiaImageHelpers
    {
        public static bool CanUseSkiaFastPath(PdfImage image)
        {
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
}
