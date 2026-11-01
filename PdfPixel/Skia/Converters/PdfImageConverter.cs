using PdfPixel.Imaging;
using PdfPixel.Imaging.Processing.Pipeline;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfPixel.Skia.Converters;

/// <summary>
/// Converts <see cref="PdfDecodedImage"/> and <see cref="PdfImageSamples"/> to <see cref="SKImage"/> for canvas rendering.
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

    /// <summary>
    /// Builds an <see cref="SKImage"/> equivalent to <paramref name="samples"/>: 8-bit alpha, gray, or RGBA with
    /// alpha last.
    /// </summary>
    public static SKImage ToSkImage(PdfImageSamples samples)
    {
        SKImageInfo imageInfo = new(samples.Width, samples.Height, GetColorType(samples), SKAlphaType.Unpremul);

        GCHandle bufferHandle = samples.PinBuffer();
        using SKPixmap pixmap = new(imageInfo, bufferHandle.AddrOfPinnedObject(), samples.RowBytes);
        return SKImage.FromPixels(pixmap, ReleaseBuffer, bufferHandle);
    }

    private static SKColorType GetColorType(PdfImageSamples samples)
    {
        IReadOnlyList<PdfImageChannel> channels = samples.Channels;

        if (samples.Format != PdfImageSampleFormat.Integer || samples.BitsPerComponent != 8)
        {
            throw new NotSupportedException("Only 8-bit integer samples can be drawn.");
        }

        if (channels.Count == 1 && channels[0].IsAlpha)
        {
            return SKColorType.Alpha8;
        }
        else if (channels.Count == 1)
        {
            return SKColorType.Gray8;
        }
        else if (channels.Count == 4 && channels[3].IsAlpha)
        {
            return SKColorType.Rgba8888;
        }
        else
        {
            throw new NotSupportedException($"Samples with {channels.Count} channels cannot be drawn.");
        }
    }

    private static void ReleaseBuffer(IntPtr pixels, object context) => ((GCHandle)context).Free();
}
