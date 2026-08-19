using PdfPixel.Imaging.Model;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Skia.Converters;

/// <summary>
/// Converts <see cref="PdfDecodedImage"/> to <see cref="SKImage"/> for canvas rendering.
/// </summary>
internal static class PdfImageConverter
{
    /// <summary>
    /// Builds an <see cref="SKImage"/> equivalent to <paramref name="image"/>.
    /// </summary>
    public static SKImage ToSkImage(PdfDecodedImage image)
    {
        SKImageInfo imageInfo = new(image.Width, image.Height, image.ColorFormat.ToSkColorType(), image.AlphaType.ToSkAlphaType());

        GCHandle bufferHandle = image.PinBuffer();
        using SKPixmap pixmap = new(imageInfo, bufferHandle.AddrOfPinnedObject(), image.RowBytes);
        return SKImage.FromPixels(pixmap, ReleaseBuffer, bufferHandle);
    }

    private static void ReleaseBuffer(IntPtr pixels, object context) => ((GCHandle)context).Free();
}
