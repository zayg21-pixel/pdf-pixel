using System;
using System.IO;
using PdfPixel.Color;
using PdfPixel.Png.Decoding;
using PdfPixel.Png.Model;
using PdfPixel.Png.Parsing;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace PdfPixel.Tests.Imaging.Png;

/// <summary>
/// Decoding tests for PNG files. Files sharing a prefix in <c>png/</c> store the same image with one
/// storage feature varied (interlacing, filter type, split image data), so a failure names the feature that
/// broke. Each decode is compared against a golden PNG in <c>png/golden/</c> produced by libpng through
/// ImageMagick, and written to <c>png/decoded/</c> in the test output directory so it can be inspected by eye.
/// </summary>
public class PngDecodingTests
{
    private const string PngFolder = "png";
    private const string GoldenFolder = "png/golden";
    private const string DecodedFolder = "png/decoded";

    /// <summary>
    /// Bytes per pixel of the buffers compared, which hold straight RGBA.
    /// </summary>
    private const int BytesPerPixel = 4;

    private readonly ITestOutputHelper _output;

    public PngDecodingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Every color type at every bit depth it allows.
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
    public void ColorTypeAndBitDepth_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Every color type and bit depth stored in the seven Adam7 passes.
    /// </summary>
    [Theory]
    [InlineData("gray1-adam7.png")]
    [InlineData("gray2-adam7.png")]
    [InlineData("gray4-adam7.png")]
    [InlineData("gray8-adam7.png")]
    [InlineData("gray16-adam7.png")]
    [InlineData("rgb8-adam7.png")]
    [InlineData("rgb16-adam7.png")]
    [InlineData("palette1-adam7.png")]
    [InlineData("palette2-adam7.png")]
    [InlineData("palette4-adam7.png")]
    [InlineData("palette8-adam7.png")]
    [InlineData("graya8-adam7.png")]
    [InlineData("graya16-adam7.png")]
    [InlineData("rgba8-adam7.png")]
    [InlineData("rgba16-adam7.png")]
    public void Adam7_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Sizes that leave Adam7 passes empty, and a size that ends part way through the 8x8 pass grid.
    /// </summary>
    [Theory]
    [InlineData("gray4-adam7-1x1.png")]
    [InlineData("gray4-adam7-2x2.png")]
    [InlineData("gray4-adam7-3x3.png")]
    [InlineData("gray4-adam7-5x5.png")]
    [InlineData("rgb8-adam7-odd.png")]
    public void Adam7EdgeSizes_DecodeToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Every row under one filter type, at 8 bits per sample and at 1 bit per pixel.
    /// </summary>
    [Theory]
    [InlineData("rgb8-filter0.png")]
    [InlineData("rgb8-filter1.png")]
    [InlineData("rgb8-filter2.png")]
    [InlineData("rgb8-filter3.png")]
    [InlineData("rgb8-filter4.png")]
    [InlineData("gray1-filter0.png")]
    [InlineData("gray1-filter1.png")]
    [InlineData("gray1-filter2.png")]
    [InlineData("gray1-filter3.png")]
    [InlineData("gray1-filter4.png")]
    public void FilterType_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// The transparent gray and color keys at 8 and 16 bits, and alpha for palette entries.
    /// </summary>
    [Theory]
    [InlineData("gray8-trns.png")]
    [InlineData("gray16-trns.png")]
    [InlineData("rgb8-trns.png")]
    [InlineData("rgb16-trns.png")]
    [InlineData("palette8-trns.png")]
    public void Transparency_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// The image data split over IDAT chunks of one byte, with an empty chunk among them, and of 1000 bytes.
    /// </summary>
    [Theory]
    [InlineData("rgb8-idat1.png")]
    [InlineData("rgb8-idat1000.png")]
    public void SplitImageData_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// A reduced decode holds every n-th pixel of every n-th row, whether the image is interlaced or not.
    /// </summary>
    [Theory]
    [InlineData("rgb8.png", 2)]
    [InlineData("rgb8.png", 8)]
    [InlineData("rgb8-adam7.png", 2)]
    [InlineData("rgb8-adam7.png", 4)]
    [InlineData("rgb8-adam7.png", 8)]
    [InlineData("gray1.png", 4)]
    [InlineData("gray1-adam7.png", 4)]
    [InlineData("palette4-adam7.png", 2)]
    [InlineData("rgb8-adam7-odd.png", 8)]
    public void Descale_PicksEveryNthPixel(string fileName, int descaleFactor)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, fileName));
        PngImageInfo info = PngReader.Read(data);

        DecodedImage decoded = Decode(info, data, descaleFactor);

        using SKBitmap golden = LoadGolden(ResolveGolden(fileName));
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();

        Assert.Equal((golden.Width + descaleFactor - 1) / descaleFactor, decoded.Width);
        Assert.Equal((golden.Height + descaleFactor - 1) / descaleFactor, decoded.Height);

        for (int y = 0; y < decoded.Height; y++)
        {
            for (int x = 0; x < decoded.Width; x++)
            {
                int goldenOffset = (y * descaleFactor * golden.RowBytes) + (x * descaleFactor * BytesPerPixel);
                int decodedOffset = ((y * decoded.Width) + x) * BytesPerPixel;

                Assert.True(
                    goldenPixels.Slice(goldenOffset, BytesPerPixel).SequenceEqual(decoded.Pixels.AsSpan(decodedOffset, BytesPerPixel)),
                    $"Pixel ({x}, {y}) of '{fileName}' at descale {descaleFactor} differs from golden pixel ({x * descaleFactor}, {y * descaleFactor}).");
            }
        }
    }

    /// <summary>
    /// The ICC profile, significant bits, background, pixel dimensions and Exif are read, and the chunks the
    /// model leaves out are skipped without affecting the image.
    /// </summary>
    [Fact]
    public void Metadata_IsRead()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, "rgb8-metadata.png"));
        PngImageInfo info = PngReader.Read(data);

        using Stream? profileStream = typeof(ProfileResources).Assembly.GetManifestResourceStream("PdfPixel.Color.EmbeddedData.CompactCmyk.icc");
        Assert.NotNull(profileStream);

        using MemoryStream expectedProfile = new();
        profileStream.CopyTo(expectedProfile);

        Assert.Equal("Test profile", info.IccProfileName);
        Assert.Equal(expectedProfile.ToArray(), info.IccProfile);
        Assert.Equal(new byte[] { 8, 7, 6 }, info.SignificantBits);

        Assert.NotNull(info.BackgroundColor);
        Assert.Equal(10, info.BackgroundColor.Value.Red);
        Assert.Equal(20, info.BackgroundColor.Value.Green);
        Assert.Equal(30, info.BackgroundColor.Value.Blue);

        Assert.NotNull(info.PhysicalDimensions);
        Assert.Equal(3780, info.PhysicalDimensions.Value.PixelsPerUnitX);
        Assert.Equal(2835, info.PhysicalDimensions.Value.PixelsPerUnitY);
        Assert.Equal(PngUnit.Meter, info.PhysicalDimensions.Value.Unit);

        Assert.NotNull(info.Exif);
        Assert.Equal((byte)'M', info.Exif[0]);

        AssertMatchesGolden("rgb8-metadata.png", Decode(info, data, 1), ResolveGolden("rgb8-metadata.png"));
    }

    /// <summary>
    /// An ancillary chunk failing its CRC is left out, and the image still decodes.
    /// </summary>
    [Fact]
    public void AncillaryCrcFailure_SkipsChunk()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, "rgb8-crc-ancillary.png"));
        PngImageInfo info = PngReader.Read(data);

        Assert.Null(info.PhysicalDimensions);

        AssertMatchesGolden("rgb8-crc-ancillary.png", Decode(info, data, 1), ResolveGolden("rgb8-crc-ancillary.png"));
    }

    /// <summary>
    /// A critical chunk failing its CRC makes the file unreadable.
    /// </summary>
    [Fact]
    public void CriticalCrcFailure_Throws()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, "rgb8-crc-critical.png"));

        Assert.Throws<InvalidDataException>(() => PngReader.Read(data));
    }

    /// <summary>
    /// A file cut off inside its image data is rejected before any row is decoded.
    /// </summary>
    [Fact]
    public void TruncatedFile_Throws()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, "rgb8-truncated.png"));

        Assert.Throws<InvalidDataException>(() => PngReader.Read(data));
    }

    /// <summary>
    /// Decodes a corpus file and requires it to reproduce its golden image sample for sample.
    /// </summary>
    /// <param name="fileName">Corpus file to decode.</param>
    private void AssertDecodesToGolden(string fileName)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(PngFolder, fileName));
        PngImageInfo info = PngReader.Read(data);

        AssertMatchesGolden(fileName, Decode(info, data, 1), ResolveGolden(fileName));
    }

    private void AssertMatchesGolden(string fileName, DecodedImage decoded, string goldenPath)
    {
        string decodedPath = SaveDecoded(fileName, decoded);
        _output.WriteLine($"Decoded {decoded.Width}x{decoded.Height} to {decodedPath}");

        using SKBitmap goldenBitmap = LoadGolden(goldenPath);

        Assert.Equal(goldenBitmap.Width, decoded.Width);
        Assert.Equal(goldenBitmap.Height, decoded.Height);

        (int maximumDifference, double meanDifference, int differingPixels) = Measure(decoded, goldenBitmap);

        _output.WriteLine(
            $"Difference from golden: max {maximumDifference}, mean {meanDifference:F4}, {differingPixels} pixel(s) differ.");

        Assert.True(
            maximumDifference == 0,
            $"File '{fileName}' differs from its golden image by up to {maximumDifference} per channel, "
                + $"mean {meanDifference:F4} over {differingPixels} pixel(s). "
                + $"Inspect {decodedPath} against {goldenPath}.");
    }

    /// <summary>
    /// Finds the golden image a corpus file is expected to decode to: one named after the file when it
    /// exists, otherwise the one named after the file's prefix.
    /// </summary>
    private static string ResolveGolden(string fileName)
    {
        string specificPath = Path.Combine(GoldenFolder, fileName);

        if (File.Exists(specificPath))
        {
            return specificPath;
        }

        int separatorIndex = fileName.IndexOf('-');
        string sourceName = (separatorIndex > 0) ? fileName.Substring(0, separatorIndex) : Path.GetFileNameWithoutExtension(fileName);
        string sharedPath = Path.Combine(GoldenFolder, sourceName + ".png");

        if (!File.Exists(sharedPath))
        {
            throw new FileNotFoundException($"No golden image for '{fileName}': looked for {specificPath} and {sharedPath}.");
        }

        return sharedPath;
    }

    /// <summary>
    /// Measures how far a decode is from the golden image over all four channels.
    /// </summary>
    private static (int MaximumDifference, double MeanDifference, int DifferingPixels) Measure(DecodedImage decoded, SKBitmap golden)
    {
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();
        ReadOnlySpan<byte> decodedPixels = decoded.Pixels;

        int maximumDifference = 0;
        long totalDifference = 0;
        int differingPixels = 0;

        for (int y = 0; y < decoded.Height; y++)
        {
            int goldenRow = y * golden.RowBytes;
            int decodedRow = y * decoded.Width * BytesPerPixel;

            for (int x = 0; x < decoded.Width; x++)
            {
                int pixelMaximum = 0;

                for (int channel = 0; channel < BytesPerPixel; channel++)
                {
                    int difference = Math.Abs(
                        decodedPixels[decodedRow + (x * BytesPerPixel) + channel]
                            - goldenPixels[goldenRow + (x * BytesPerPixel) + channel]);

                    pixelMaximum = Math.Max(pixelMaximum, difference);
                    totalDifference += difference;
                }

                if (pixelMaximum > 0)
                {
                    differingPixels++;
                }

                maximumDifference = Math.Max(maximumDifference, pixelMaximum);
            }
        }

        double meanDifference = (double)totalDifference / ((long)decoded.Width * decoded.Height * BytesPerPixel);

        return (maximumDifference, meanDifference, differingPixels);
    }

    /// <summary>
    /// Decodes the golden PNG into straight RGBA bytes so its samples can be read directly.
    /// </summary>
    private static SKBitmap LoadGolden(string goldenPath)
    {
        using SKCodec codec = SKCodec.Create(goldenPath)
            ?? throw new InvalidDataException($"Golden image could not be opened: {goldenPath}");

        SKImageInfo info = new(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap bitmap = SKBitmap.Decode(codec, info);

        if (bitmap == null)
        {
            throw new InvalidDataException($"Golden image could not be decoded: {goldenPath}");
        }

        return bitmap;
    }

    /// <summary>
    /// Writes the decoded samples out as a PNG so a failing comparison can be examined by eye.
    /// </summary>
    private static string SaveDecoded(string fileName, DecodedImage decoded)
    {
        Directory.CreateDirectory(DecodedFolder);

        string decodedPath = Path.GetFullPath(Path.Combine(DecodedFolder, fileName));

        SKImageInfo info = new(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using SKImage image = SKImage.FromPixelCopy(info, decoded.Pixels);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(decodedPath);
        data.SaveTo(output);

        return decodedPath;
    }

    /// <summary>
    /// Runs the PNG row decoder over a file and maps its samples onto straight RGBA the way libpng expands
    /// them to 8 bits, applying the palette and the transparency chunk.
    /// </summary>
    internal static DecodedImage Decode(PngImageInfo info, byte[] data, int descaleFactor)
    {
        PngRowDecoder decoder = new(info, data, descaleFactor);

        byte[] rowBuffer = new byte[decoder.RowBytes];
        byte[] pixels = new byte[decoder.Width * decoder.Height * BytesPerPixel];
        int[] samples = new int[decoder.ComponentCount];

        int row = 0;
        while (decoder.TryReadRow(rowBuffer))
        {
            for (int x = 0; x < decoder.Width; x++)
            {
                for (int component = 0; component < decoder.ComponentCount; component++)
                {
                    samples[component] = ReadSample(rowBuffer, (x * decoder.ComponentCount) + component, decoder.BitsPerComponent);
                }

                int destination = ((row * decoder.Width) + x) * BytesPerPixel;
                WriteRgba(info, decoder.BitsPerComponent, samples, pixels.AsSpan(destination, BytesPerPixel));
            }

            row++;
        }

        Assert.Equal(decoder.Height, row);

        return new DecodedImage(decoder.Width, decoder.Height, pixels);
    }

    private static void WriteRgba(PngImageInfo info, int bits, int[] samples, Span<byte> rgba)
    {
        switch (info.ColorType)
        {
            case PngColorType.Grayscale:
                {
                    byte gray = ToByte(samples[0], bits);
                    rgba[0] = gray;
                    rgba[1] = gray;
                    rgba[2] = gray;
                    rgba[3] = (info.TransparentGray == samples[0]) ? (byte)0 : byte.MaxValue;
                    break;
                }
            case PngColorType.Truecolor:
                {
                    rgba[0] = ToByte(samples[0], bits);
                    rgba[1] = ToByte(samples[1], bits);
                    rgba[2] = ToByte(samples[2], bits);

                    bool isTransparent = info.TransparentColor != null
                        && info.TransparentColor.Value.Red == samples[0]
                        && info.TransparentColor.Value.Green == samples[1]
                        && info.TransparentColor.Value.Blue == samples[2];

                    rgba[3] = isTransparent ? (byte)0 : byte.MaxValue;
                    break;
                }
            case PngColorType.IndexedColor:
                {
                    byte[] palette = info.Palette ?? throw new InvalidDataException("Indexed-color image has no palette.");
                    byte[] paletteAlpha = info.PaletteAlpha ?? [];
                    int index = samples[0];

                    rgba[0] = palette[index * 3];
                    rgba[1] = palette[(index * 3) + 1];
                    rgba[2] = palette[(index * 3) + 2];
                    rgba[3] = (index < paletteAlpha.Length) ? paletteAlpha[index] : byte.MaxValue;
                    break;
                }
            case PngColorType.GrayscaleAlpha:
                {
                    byte gray = ToByte(samples[0], bits);
                    rgba[0] = gray;
                    rgba[1] = gray;
                    rgba[2] = gray;
                    rgba[3] = ToByte(samples[1], bits);
                    break;
                }
            default:
                {
                    rgba[0] = ToByte(samples[0], bits);
                    rgba[1] = ToByte(samples[1], bits);
                    rgba[2] = ToByte(samples[2], bits);
                    rgba[3] = ToByte(samples[3], bits);
                    break;
                }
        }
    }

    private static int ReadSample(byte[] row, int sampleIndex, int bitsPerComponent)
    {
        int bitOffset = sampleIndex * bitsPerComponent;
        int byteIndex = bitOffset >> 3;
        int usedBits = (bitOffset & 7) + bitsPerComponent;
        int byteCount = (usedBits + 7) >> 3;

        int window = 0;
        for (int index = 0; index < byteCount; index++)
        {
            window = (window << 8) | row[byteIndex + index];
        }

        return (window >> ((byteCount * 8) - usedBits)) & ((1 << bitsPerComponent) - 1);
    }

    private static byte ToByte(int sample, int bitsPerComponent)
    {
        int maximum = (1 << bitsPerComponent) - 1;
        return (byte)(((sample * 255) + (maximum / 2)) / maximum);
    }

    /// <summary>
    /// A decoded corpus image as straight RGBA, the form both the comparison and the PNG snapshot consume.
    /// </summary>
    internal sealed record DecodedImage(int Width, int Height, byte[] Pixels);
}
