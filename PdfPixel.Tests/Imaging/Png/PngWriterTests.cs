using System;
using System.IO;
using System.IO.Compression;
using PdfPixel.Png;
using SkiaSharp;
using Xunit;

namespace PdfPixel.Tests.Imaging.Png;

/// <summary>
/// Encoding tests for the PNG writer. Corpus images are decoded, written back with each filtering choice and
/// interlace method, and the written file must decode to the same samples with the PNG decoder and to the
/// corpus golden with Skia's own PNG decoder.
/// </summary>
public class PngWriterTests
{
    private const string PngFolder = "png";
    private const string GoldenFolder = "png/golden";

    /// <summary>
    /// Every color type and bit depth, with adaptive filtering on and off, stored progressively and in Adam7 passes.
    /// </summary>
    [Theory]
    [InlineData("gray1.png")]
    [InlineData("gray2.png")]
    [InlineData("gray4.png")]
    [InlineData("gray8.png")]
    [InlineData("gray16.png")]
    [InlineData("rgb8.png")]
    [InlineData("rgb16.png")]
    [InlineData("palette1.png")]
    [InlineData("palette2.png")]
    [InlineData("palette4.png")]
    [InlineData("palette8.png")]
    [InlineData("graya8.png")]
    [InlineData("graya16.png")]
    [InlineData("rgba8.png")]
    [InlineData("rgba16.png")]
    [InlineData("gray8-trns.png")]
    [InlineData("rgb16-trns.png")]
    [InlineData("palette8-trns.png")]
    [InlineData("rgb8-adam7-odd.png")]
    [InlineData("gray4-adam7-3x3.png")]
    public void RoundTrip_ReproducesSamples(string fileName)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, fileName));
        PngImageInfo sourceInfo = PngImageInfo.Parse(data);
        (byte[] samples, int rowBytes) = ReadSamples(sourceInfo, data);

        foreach (bool adaptiveFiltering in new[] { false, true })
        {
            foreach (PngInterlaceMethod interlaceMethod in new[] { PngInterlaceMethod.None, PngInterlaceMethod.Adam7 })
            {
                string variant = $"{fileName}, adaptive filtering {adaptiveFiltering}, interlace {interlaceMethod}";

                PngImageInfo writtenInfo = CopyHeader(sourceInfo);
                writtenInfo.InterlaceMethod = interlaceMethod;

                using MemoryStream written = new();
                PngWriter.Write(written, writtenInfo, samples, rowBytes, adaptiveFiltering, CompressionLevel.Optimal);
                byte[] writtenBytes = written.ToArray();

                PngImageInfo readBackInfo = PngImageInfo.Parse(writtenBytes);
                Assert.Equal(interlaceMethod, readBackInfo.InterlaceMethod);

                (byte[] readBackSamples, _) = ReadSamples(readBackInfo, writtenBytes);
                Assert.True(samples.AsSpan().SequenceEqual(readBackSamples), $"Samples differ after writing {variant}.");

                AssertSkiaDecodesToGolden(writtenBytes, fileName, variant);
            }
        }
    }

    /// <summary>
    /// The ICC profile, significant bits, background, pixel dimensions and Exif are written and read back.
    /// </summary>
    [Fact]
    public void Metadata_RoundTrips()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, "rgb8-metadata.png"));
        PngImageInfo sourceInfo = PngImageInfo.Parse(data);
        (byte[] samples, int rowBytes) = ReadSamples(sourceInfo, data);

        using MemoryStream written = new();
        PngWriter.Write(written, sourceInfo, samples, rowBytes, adaptiveFiltering: true, CompressionLevel.Fastest);
        PngImageInfo readBackInfo = PngImageInfo.Parse(written.ToArray());

        Assert.Equal(sourceInfo.IccProfileName, readBackInfo.IccProfileName);
        Assert.Equal(sourceInfo.IccProfile, readBackInfo.IccProfile);
        Assert.Equal(sourceInfo.SignificantBits, readBackInfo.SignificantBits);
        Assert.Equal(sourceInfo.BackgroundColor?.Red, readBackInfo.BackgroundColor?.Red);
        Assert.Equal(sourceInfo.BackgroundColor?.Green, readBackInfo.BackgroundColor?.Green);
        Assert.Equal(sourceInfo.BackgroundColor?.Blue, readBackInfo.BackgroundColor?.Blue);
        Assert.Equal(sourceInfo.PhysicalDimensions?.PixelsPerUnitX, readBackInfo.PhysicalDimensions?.PixelsPerUnitX);
        Assert.Equal(sourceInfo.PhysicalDimensions?.PixelsPerUnitY, readBackInfo.PhysicalDimensions?.PixelsPerUnitY);
        Assert.Equal(sourceInfo.PhysicalDimensions?.Unit, readBackInfo.PhysicalDimensions?.Unit);
        Assert.Equal(sourceInfo.Exif, readBackInfo.Exif);
    }

    /// <summary>
    /// An indexed-color image cannot be written without its palette.
    /// </summary>
    [Fact]
    public void IndexedColorWithoutPalette_Throws()
    {
        PngImageInfo info = new() { Width = 2, Height = 2, BitDepth = 8, ColorType = PngColorType.IndexedColor };
        byte[] samples = new byte[4];

        using MemoryStream written = new();

        Assert.Throws<ArgumentException>(() => PngWriter.Write(written, info, samples, 2, adaptiveFiltering: true, CompressionLevel.Optimal));
    }

    /// <summary>
    /// Decodes every row of an image into one buffer of packed rows.
    /// </summary>
    private static (byte[] Samples, int RowBytes) ReadSamples(PngImageInfo info, byte[] data)
    {
        PngRowDecoder decoder = new(info, data);
        var samples = new byte[decoder.RowBytes * decoder.Height];

        for (int row = 0; row < decoder.Height; row++)
        {
            Assert.True(decoder.TryReadRow(samples.AsSpan(row * decoder.RowBytes, decoder.RowBytes)));
        }

        return (samples, decoder.RowBytes);
    }

    /// <summary>
    /// Copies the header, palette and transparency of an image into a new info.
    /// </summary>
    private static PngImageInfo CopyHeader(PngImageInfo source)
    {
        return new PngImageInfo
        {
            Width = source.Width,
            Height = source.Height,
            BitDepth = source.BitDepth,
            ColorType = source.ColorType,
            Palette = source.Palette,
            PaletteAlpha = source.PaletteAlpha,
            TransparentGray = source.TransparentGray,
            TransparentColor = source.TransparentColor
        };
    }

    /// <summary>
    /// Decodes a written file with Skia and requires it to match the golden of the corpus file it came from.
    /// 16-bit samples are allowed one step of difference for Skia's own reduction to 8 bits.
    /// </summary>
    private static void AssertSkiaDecodesToGolden(byte[] png, string fileName, string variant)
    {
        using SKData encoded = SKData.CreateCopy(png);
        using SKCodec codec = SKCodec.Create(encoded) ?? throw new InvalidDataException($"Skia could not open {variant}.");

        SKImageInfo imageInfo = new(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using SKBitmap decoded = SKBitmap.Decode(codec, imageInfo) ?? throw new InvalidDataException($"Skia could not decode {variant}.");

        string goldenPath = Path.Combine(GoldenFolder, fileName);
        if (!File.Exists(goldenPath))
        {
            goldenPath = Path.Combine(GoldenFolder, fileName.Substring(0, fileName.IndexOf('-')) + ".png");
        }

        using SKCodec goldenCodec = SKCodec.Create(goldenPath) ?? throw new InvalidDataException($"Golden could not be opened: {goldenPath}");
        using SKBitmap golden = SKBitmap.Decode(goldenCodec, new SKImageInfo(goldenCodec.Info.Width, goldenCodec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul));

        Assert.Equal(golden.Width, decoded.Width);
        Assert.Equal(golden.Height, decoded.Height);

        int tolerance = fileName.Contains("16", StringComparison.Ordinal) ? 1 : 0;
        ReadOnlySpan<byte> decodedPixels = decoded.GetPixelSpan();
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();
        int maximumDifference = 0;

        for (int index = 0; index < goldenPixels.Length; index++)
        {
            maximumDifference = Math.Max(maximumDifference, Math.Abs(decodedPixels[index] - goldenPixels[index]));
        }

        Assert.True(maximumDifference <= tolerance, $"Skia decodes {variant} up to {maximumDifference} away from {goldenPath}.");
    }
}
