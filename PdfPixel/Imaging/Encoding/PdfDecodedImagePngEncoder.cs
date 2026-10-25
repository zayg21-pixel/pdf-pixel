using PdfPixel.Color;
using PdfPixel.Png.Encoding;
using PdfPixel.Png.Model;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Encoding;

/// <summary>
/// Encodes a <see cref="PdfDecodedImage"/> as an 8-bit PNG file.
/// </summary>
internal static class PdfDecodedImagePngEncoder
{
    /// <summary>
    /// Entry <c>a</c> holds 255 / a in 16.16 fixed point, rounded up; entry 0 is unused.
    /// </summary>
    private static readonly uint[] UnpremultiplyScale = BuildUnpremultiplyScale();

    /// <summary>
    /// Encodes <paramref name="decoded"/> as a PNG file with unpremultiplied alpha, rewriting its pixels in place.
    /// An <see cref="PdfImageColorFormat.Alpha"/> image is stored as grayscale with alpha, its gray samples 0.
    /// </summary>
    public static byte[] Encode(PdfDecodedImage decoded)
    {
        PngImageInfo info = new() { Width = decoded.Width, Height = decoded.Height, BitDepth = 8 };

        ReadOnlySpan<byte> pixels;
        int rowBytes;

        if (decoded.ColorFormat == PdfImageColorFormat.Gray)
        {
            info.ColorType = PngColorType.Grayscale;
            pixels = decoded.GetPixels();
            rowBytes = decoded.RowBytes;
        }
        else if (decoded.ColorFormat == PdfImageColorFormat.Alpha)
        {
            info.ColorType = PngColorType.GrayscaleAlpha;
            pixels = ExpandAlpha(decoded);
            rowBytes = decoded.Width * 2;
        }
        else if (decoded.AlphaType == PdfImageAlphaType.Opaque)
        {
            info.ColorType = PngColorType.Truecolor;
            pixels = DropAlpha(decoded);
            rowBytes = decoded.Width * 3;
        }
        else if (decoded.AlphaType == PdfImageAlphaType.Premultiplied)
        {
            info.ColorType = PngColorType.TruecolorAlpha;
            pixels = Unpremultiply(decoded);
            rowBytes = decoded.RowBytes;
        }
        else
        {
            info.ColorType = PngColorType.TruecolorAlpha;
            pixels = decoded.GetPixels();
            rowBytes = decoded.RowBytes;
        }

        using MemoryStream output = new();
        PngWriter.Write(output, info, pixels, rowBytes, adaptiveFiltering: true, CompressionLevel.Optimal);

        return output.ToArray();
    }

    /// <summary>
    /// Returns gray and alpha sample pairs of an <see cref="PdfImageColorFormat.Alpha"/> image, every gray sample 0.
    /// </summary>
    private static byte[] ExpandAlpha(PdfDecodedImage decoded)
    {
        ReadOnlySpan<byte> alpha = decoded.GetPixels();
        var pixels = new byte[alpha.Length * 2];

        for (int index = 0; index < alpha.Length; index++)
        {
            pixels[(index * 2) + 1] = alpha[index];
        }

        return pixels;
    }

    /// <summary>
    /// Packs the RGB samples of an <see cref="PdfImageColorFormat.Rgba"/> image to the start of its own buffer
    /// and returns them.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static Span<byte> DropAlpha(PdfDecodedImage decoded)
    {
        Span<byte> buffer = decoded.GetRawBuffer();
        int pixelCount = buffer.Length / 4;
        ref RgbaPacked source = ref Unsafe.As<byte, RgbaPacked>(ref buffer[0]);
        ref RgbPacked target = ref Unsafe.As<byte, RgbPacked>(ref buffer[0]);

        for (int index = 0; index < pixelCount; index++)
        {
            RgbaPacked pixel = source;
            target = new RgbPacked(pixel.R, pixel.G, pixel.B);

            source = ref Unsafe.Add(ref source, 1);
            target = ref Unsafe.Add(ref target, 1);
        }

        return buffer.Slice(0, pixelCount * 3);
    }

    /// <summary>
    /// Divides the color samples of a premultiplied image by their alpha in its own buffer and returns them.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private static Span<byte> Unpremultiply(PdfDecodedImage decoded)
    {
        Span<byte> buffer = decoded.GetRawBuffer();
        int pixelCount = buffer.Length / 4;
        ref RgbaPacked pixel = ref Unsafe.As<byte, RgbaPacked>(ref buffer[0]);

        for (int index = 0; index < pixelCount; index++)
        {
            int alpha = pixel.A;

            if (alpha == 0)
            {
                pixel = default;
            }
            else if (alpha != byte.MaxValue)
            {
                uint factor = UnpremultiplyScale[alpha];
                pixel.R = (byte)Math.Min(byte.MaxValue, ((pixel.R * factor) + 0x8000) >> 16);
                pixel.G = (byte)Math.Min(byte.MaxValue, ((pixel.G * factor) + 0x8000) >> 16);
                pixel.B = (byte)Math.Min(byte.MaxValue, ((pixel.B * factor) + 0x8000) >> 16);
            }

            pixel = ref Unsafe.Add(ref pixel, 1);
        }

        return buffer;
    }

    private static uint[] BuildUnpremultiplyScale()
    {
        var scale = new uint[256];

        for (int alpha = 1; alpha < scale.Length; alpha++)
        {
            scale[alpha] = (uint)(((byte.MaxValue << 16) + alpha - 1) / alpha);
        }

        return scale;
    }
}
