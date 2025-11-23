using PdfReader.Color.Icc.Model;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using SkiaSharp;

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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="image"></param>
    /// <param name="icc"></param>
    /// <returns></returns>
    public static SKImage UpdateIccColorSpace(SKImage image, IccProfile icc)
    {
        if (image == null)
        {
            return null;
        }

        using var iccProfile = SKColorSpace.CreateIcc(icc.Bytes) ?? SKColorSpace.CreateSrgb();
        SKImageInfo info = new SKImageInfo(image.Width, image.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul, iccProfile);
        // this is the important part. set the destination `ColorSpace` as
        // `SKColorSpace.CreateSrgb()`. Skia will then automatically convert the original CMYK
        // colorspace, to this new sRGB colorspace. (Though the conversion is extremely slow!
        // More on this in at the end of this post.)
        SKImage newImg = SKImage.Create(info);
        image.ReadPixels(newImg.PeekPixels());

        return newImg;

    }
}
