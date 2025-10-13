using PdfReader.Rendering.Color;
using PdfReader.Rendering.Image.Processing;
using SkiaSharp;
using System.IO;

namespace PdfReader.Rendering.Image.Skia
{
    internal class SkiaImageHelpers
    {
        public static bool CanUseSkiaFastPath(PdfImage image)
        {
            return false;
            if (image.MaskArray?.Length > 0)
            {
                return false;
            }

            var normalizedDecode = ProcessingUtilities.BuildDecodeMinSpanBytes(image.ColorSpaceConverter.Components, image.DecodeArray, image.HasImageMask);

            if (normalizedDecode != null)
            {
                return false;
            }

            if (image.BitsPerComponent != 8) // TODO: Skia might actually support 16 bit correctly, need to verify.
            {
                return false;
            }

            if (image.ColorSpaceConverter.Components != 1 && image.ColorSpaceConverter.Components != 3)
            {
                return false;
            }

            if (image.ColorSpaceConverter is not DeviceRgbConverter && image.ColorSpaceConverter is not DeviceGrayConverter && image.ColorSpaceConverter is not IccBasedConverter)
            {
                return false;
            }

            return true;
        }

        public static SKImage DecodeWithSkiaUsingIcc(Stream stream, IccBasedConverter iccConverter)
        {
            using var data = SKData.Create(stream);
            using SKCodec codec = SKCodec.Create(data);

            using SKColorSpace sourceColorSpace = SKColorSpace.CreateIcc(iccConverter.IccBytes);

            var sourceInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul, sourceColorSpace);
            using var bitmap = new SKBitmap(sourceInfo);

            if (codec.GetPixels(sourceInfo, bitmap.GetPixels()) == SKCodecResult.Success)
            {
                var result = SKImage.FromBitmap(bitmap);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
