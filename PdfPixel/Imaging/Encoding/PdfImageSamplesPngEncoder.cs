using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Png;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace PdfPixel.Imaging.Encoding;

/// <summary>
/// Encodes 8-bit <see cref="PdfImageSamples"/> as a PNG file.
/// </summary>
internal static class PdfImageSamplesPngEncoder
{
    private const int RgbaChannelCount = 4;
    private const byte OpaqueAlpha = byte.MaxValue;

    /// <summary>
    /// Encodes <paramref name="samples"/> as a PNG file with unpremultiplied alpha. Alpha-only samples are stored as
    /// grayscale with alpha, their gray samples 0; RGBA samples that are opaque everywhere are stored without alpha.
    /// </summary>
    public static byte[] Encode(PdfImageSamples samples)
    {
        if (samples.Format != PdfImageSampleFormat.Integer || samples.BitsPerComponent != 8)
        {
            throw new NotSupportedException("Only 8-bit integer samples can be encoded.");
        }

        IReadOnlyList<PdfImageChannel> channels = samples.Channels;
        PngImageInfo info = new() { Width = samples.Width, Height = samples.Height, BitDepth = 8 };

        ReadOnlySpan<byte> pixels;
        int rowBytes;

        if (channels.Count == 1 && channels[0].IsAlpha)
        {
            info.ColorType = PngColorType.GrayscaleAlpha;
            pixels = ExpandAlpha(samples);
            rowBytes = samples.Width * 2;
        }
        else if (channels.Count == 1)
        {
            info.ColorType = PngColorType.Grayscale;
            pixels = samples.Pixels;
            rowBytes = samples.RowBytes;
        }
        else if (channels.Count == RgbaChannelCount && IsOpaque(samples))
        {
            info.ColorType = PngColorType.Truecolor;
            pixels = DropAlpha(samples);
            rowBytes = samples.Width * 3;
        }
        else if (channels.Count == RgbaChannelCount)
        {
            info.ColorType = PngColorType.TruecolorAlpha;
            pixels = samples.Pixels;
            rowBytes = samples.RowBytes;
        }
        else
        {
            throw new NotSupportedException($"Samples with {channels.Count} channels cannot be encoded.");
        }

        using MemoryStream output = new();
        PngWriter.Write(output, info, pixels, rowBytes, adaptiveFiltering: true, CompressionLevel.Optimal);

        return output.ToArray();
    }

    /// <summary>
    /// Returns gray and alpha sample pairs of alpha-only samples, every gray sample 0.
    /// </summary>
    private static byte[] ExpandAlpha(PdfImageSamples samples)
    {
        ReadOnlySpan<byte> alpha = samples.Pixels;
        var pixels = new byte[alpha.Length * 2];

        for (int index = 0; index < alpha.Length; index++)
        {
            pixels[(index * 2) + 1] = alpha[index];
        }

        return pixels;
    }

    private static bool IsOpaque(PdfImageSamples samples)
    {
        ReadOnlySpan<byte> pixels = samples.Pixels;

        for (int index = RgbaChannelCount - 1; index < pixels.Length; index += RgbaChannelCount)
        {
            if (pixels[index] != OpaqueAlpha)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the RGB samples of RGBA samples.
    /// </summary>
    private static byte[] DropAlpha(PdfImageSamples samples)
    {
        ReadOnlySpan<byte> source = samples.Pixels;
        int pixelCount = source.Length / RgbaChannelCount;
        var pixels = new byte[pixelCount * 3];

        for (int index = 0; index < pixelCount; index++)
        {
            int sourceOffset = index * RgbaChannelCount;
            int targetOffset = index * 3;
            pixels[targetOffset] = source[sourceOffset];
            pixels[targetOffset + 1] = source[sourceOffset + 1];
            pixels[targetOffset + 2] = source[sourceOffset + 2];
        }

        return pixels;
    }
}
