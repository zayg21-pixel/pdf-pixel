using System;
using System.Collections.Generic;
using System.IO;
using PdfPixel.Color.Profiles;
using PdfPixel.Tiff.Decoding;
using PdfPixel.Tiff.Model;
using PdfPixel.Tiff.Parsing;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace PdfPixel.Tests.Imaging.Tiff;

/// <summary>
/// Decoding tests for TIFF files. Files sharing a prefix in <c>tiff/</c> store the same image with one
/// layout or compression feature varied, so a failure names the feature that broke. Each decode is
/// compared against a golden PNG in <c>tiff/golden/</c> produced by libtiff through ImageMagick, except the
/// JPEG files, whose goldens are PdfPixel's own decodes. Each decode is written to <c>tiff/decoded/</c> in
/// the test output directory so it can be inspected by eye afterwards.
/// </summary>
public class TiffDecodingTests
{
    private const string TiffFolder = "tiff";
    private const string GoldenFolder = "tiff/golden";
    private const string DecodedFolder = "tiff/decoded";

    /// <summary>
    /// Bytes per pixel of the buffers compared, which hold straight RGBA.
    /// </summary>
    private const int BytesPerPixel = 4;

    private readonly ITestOutputHelper _output;

    public TiffDecodingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Every lossless compression scheme, with and without the horizontal predictor.
    /// </summary>
    [Theory]
    [InlineData("baboon-none.tif")]
    [InlineData("baboon-lzw.tif")]
    [InlineData("baboon-deflate.tif")]
    [InlineData("baboon-packbits.tif")]
    [InlineData("baboon-lzw-predictor.tif")]
    [InlineData("baboon-deflate-predictor.tif")]
    [InlineData("baboon-packbits-noop.tif")]
    public void Compression_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// JPEG 2000 codestreams per tile under the Aperio RGB code and per strip under the generic code.
    /// </summary>
    [Theory]
    [InlineData("baboon-jpeg2000-aperio-tiles.tif")]
    [InlineData("baboon-jpeg2000-strips.tif")]
    public void Jpeg2000_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Strip heights from one row to the whole image, and a last strip shorter than the others.
    /// </summary>
    [Theory]
    [InlineData("baboon-strip-rows-1.tif")]
    [InlineData("baboon-strip-single.tif")]
    public void StripLayout_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Tiles that divide the image evenly and tiles that leave ragged edges on the right and bottom.
    /// </summary>
    [Theory]
    [InlineData("baboon-tiles-64.tif")]
    [InlineData("baboon-tiles-ragged.tif")]
    [InlineData("baboon-tiles-planar.tif")]
    [InlineData("gray-tiles.tif")]
    [InlineData("gray4-tiles.tif")]
    [InlineData("palette8-tiles.tif")]
    public void TileLayout_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Each sample stored in its own plane of strips.
    /// </summary>
    [Theory]
    [InlineData("baboon-planar.tif")]
    [InlineData("baboon-planar-lzw-predictor.tif")]
    [InlineData("rgba-planar.tif")]
    public void PlanarConfiguration_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Big-endian files and the BigTIFF container with its 64-bit offsets.
    /// </summary>
    [Theory]
    [InlineData("baboon-msb.tif")]
    [InlineData("baboon-msb-lzw-predictor.tif")]
    [InlineData("baboon-bigtiff.tif")]
    public void Container_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// 16-bit samples in either byte order, including the predictor, which differences whole 16-bit values.
    /// </summary>
    [Theory]
    [InlineData("rgb16-none.tif")]
    [InlineData("rgb16-msb.tif")]
    [InlineData("rgb16-lzw-predictor.tif")]
    [InlineData("rgb16-msb-deflate-predictor.tif")]
    [InlineData("gray16-deflate-predictor.tif")]
    [InlineData("gray16-msb.tif")]
    public void SixteenBitSamples_DecodeToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Depths other than 1, 2, 4, 8 and 16, normalized to at most 16 bits, including 32-bit samples in either
    /// byte order and under the predictor, 12-bit samples crossing byte boundaries, and 5-6-5 RGB.
    /// </summary>
    [Theory]
    [InlineData("baboon-12bit.tif")]
    [InlineData("baboon-24bit.tif")]
    [InlineData("baboon-32bit.tif")]
    [InlineData("baboon-32bit-msb.tif")]
    [InlineData("baboon-32bit-lzw-predictor.tif")]
    [InlineData("gray12-none.tif")]
    [InlineData("rgb565-none.tif")]
    public void OtherIntegerDepths_DecodeToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Two's complement samples, offset to unsigned.
    /// </summary>
    [Theory]
    [InlineData("baboon-signed8.tif")]
    [InlineData("baboon-signed16.tif")]
    [InlineData("baboon-signed16-planar.tif")]
    public void SignedSamples_DecodeToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Half, single and double precision samples clamped to 0 to 1, under the floating-point predictor in either
    /// byte order, and with values outside 0 to 1.
    /// </summary>
    [Theory]
    [InlineData("baboon-float16.tif")]
    [InlineData("baboon-float32.tif")]
    [InlineData("baboon-float64.tif")]
    [InlineData("baboon-float16-deflate-fp.tif")]
    [InlineData("baboon-float32-deflate-fp.tif")]
    [InlineData("baboon-float32-msb-lzw-fp.tif")]
    [InlineData("floatrange-none.tif")]
    public void FloatingPointSamples_DecodeToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// YCbCr outside JPEG converted to RGB: every chroma subsampling in strips and tiles, a size that ends
    /// part way through a block, planar samples, and non-default coefficients and reference black and white.
    /// </summary>
    [Theory]
    [InlineData("ycbcr-none.tif")]
    [InlineData("ycbcr-422.tif")]
    [InlineData("ycbcr-420.tif")]
    [InlineData("ycbcr-411.tif")]
    [InlineData("ycbcr-420-tiles.tif")]
    [InlineData("ycbcr-420-odd.tif")]
    [InlineData("ycbcr-420-studio.tif")]
    [InlineData("ycbcr-planar.tif")]
    public void YCbCr_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Grayscale at every packed bit depth, and with zero as white.
    /// </summary>
    [Theory]
    [InlineData("gray-none.tif")]
    [InlineData("gray-lzw-predictor.tif")]
    [InlineData("gray-miniswhite.tif")]
    [InlineData("gray4-none.tif")]
    [InlineData("gray4-lzw.tif")]
    [InlineData("gray2-packbits.tif")]
    public void Grayscale_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Palette indices of 8 and 4 bits, CMYK separations and unassociated alpha.
    /// </summary>
    [Theory]
    [InlineData("palette8-lzw.tif")]
    [InlineData("palette4-none.tif")]
    [InlineData("cmyk-lzw-predictor.tif")]
    [InlineData("cmyk-icc.tif")]
    [InlineData("rgba-lzw-predictor.tif")]
    public void ColorModel_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// Bi-level images under every CCITT scheme, the general-purpose schemes, the reversed fill order and tiles.
    /// </summary>
    [Theory]
    [InlineData("fax-none.tif")]
    [InlineData("fax-packbits.tif")]
    [InlineData("fax-lzw.tif")]
    [InlineData("fax-rle.tif")]
    [InlineData("fax-g3.tif")]
    [InlineData("fax-g3-2d.tif")]
    [InlineData("fax-g4.tif")]
    [InlineData("fax-g4-single.tif")]
    [InlineData("fax-g4-lsb.tif")]
    [InlineData("fax-g4-miniswhite.tif")]
    [InlineData("fax-g4-tiles.tif")]
    public void Bilevel_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// JPEG strips and tiles sharing their tables through JPEGTables, stored as RGB and as YCbCr.
    /// </summary>
    [Theory]
    [InlineData("jpeg-strips.tif")]
    [InlineData("jpeg-tiles.tif")]
    [InlineData("jpeg-ycbcr.tif")]
    public void Jpeg_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// The original TIFF 6.0 JPEG scheme: a strip behind a JPEGInterchangeFormat stream, and tiles holding only
    /// scan data with their tables in JPEGQTables, JPEGDCTables and JPEGACTables. Both are samples from libtiff's
    /// test images.
    /// </summary>
    [Theory]
    [InlineData("oldjpeg-smallliz.tif")]
    [InlineData("oldjpeg-zackthecat.tif")]
    public void OldJpeg_DecodesToGolden(string fileName) => AssertDecodesToGolden(fileName);

    /// <summary>
    /// The second image of a file is reached through the first directory's next-directory offset.
    /// </summary>
    [Fact]
    public void Multipage_DecodesEachDirectory()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TiffFolder, "baboon-multipage.tif"));
        List<TiffDirectory> directories = TiffReader.ReadDirectories(data);

        Assert.Equal(2, directories.Count);

        AssertMatchesGolden("baboon-multipage.tif", Decode(directories[0], data), ResolveGolden("baboon-multipage.tif"));
        AssertMatchesGolden("baboon-multipage-page1.tif", Decode(directories[1], data), ResolveGolden("baboon-multipage-page1.tif"));
    }

    /// <summary>
    /// A pyramid marks every level below the full image as reduced resolution.
    /// </summary>
    [Fact]
    public void Pyramid_MarksReducedResolutionLevels()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TiffFolder, "baboon-pyramid.tif"));
        List<TiffDirectory> directories = TiffReader.ReadDirectories(data);

        Assert.Equal(3, directories.Count);
        Assert.Equal(TiffSubfileType.None, directories[0].SubfileType);
        Assert.Equal(TiffSubfileType.ReducedResolution, directories[1].SubfileType);
        Assert.Equal(TiffSubfileType.ReducedResolution, directories[2].SubfileType);

        AssertMatchesGolden("baboon-pyramid.tif", Decode(directories[0], data), ResolveGolden("baboon-pyramid.tif"));
        AssertMatchesGolden("baboon-pyramid-page1.tif", Decode(directories[1], data), ResolveGolden("baboon-pyramid-page1.tif"));
        AssertMatchesGolden("baboon-pyramid-page2.tif", Decode(directories[2], data), ResolveGolden("baboon-pyramid-page2.tif"));
    }

    /// <summary>
    /// A directory reached only through SubIFDs is a child of its parent, not part of the main chain.
    /// </summary>
    [Fact]
    public void SubIfds_AreReadAsChildDirectories()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TiffFolder, "baboon-subifd.tif"));
        List<TiffDirectory> directories = TiffReader.ReadDirectories(data);

        Assert.Single(directories);
        TiffDirectory child = Assert.Single(directories[0].SubDirectories);
        Assert.Equal(TiffSubfileType.ReducedResolution, child.SubfileType);

        AssertMatchesGolden("baboon-subifd.tif", Decode(directories[0], data), ResolveGolden("baboon-subifd.tif"));
        AssertMatchesGolden("baboon-subifd-child.tif", Decode(child, data), ResolveGolden("baboon-subifd-child.tif"));
    }

    /// <summary>
    /// The embedded ICC profile is returned byte for byte.
    /// </summary>
    [Fact]
    public void IccProfile_IsReadByteForByte()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TiffFolder, "cmyk-icc.tif"));
        List<TiffDirectory> directories = TiffReader.ReadDirectories(data);

        using Stream? profileStream = typeof(ProfileRespources).Assembly.GetManifestResourceStream("PdfPixel.Color.Profiles.CompactCmyk.icc");
        Assert.NotNull(profileStream);

        using MemoryStream expectedProfile = new();
        profileStream.CopyTo(expectedProfile);

        Assert.Equal(expectedProfile.ToArray(), directories[0].IccProfile);
    }

    /// <summary>
    /// The orientation is reported, and the stored rows are produced as stored.
    /// </summary>
    [Fact]
    public void Orientation_IsReported()
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TiffFolder, "baboon-orientation.tif"));
        List<TiffDirectory> directories = TiffReader.ReadDirectories(data);

        Assert.Equal(TiffOrientation.RightTop, directories[0].Orientation);

        AssertMatchesGolden("baboon-orientation.tif", Decode(directories[0], data), ResolveGolden("baboon-orientation.tif"));
    }

    /// <summary>
    /// Decodes the first image of a corpus file and requires it to reproduce its golden image sample for sample.
    /// </summary>
    /// <param name="fileName">Corpus file to decode.</param>
    private void AssertDecodesToGolden(string fileName)
    {
        byte[] data = File.ReadAllBytes(Path.Combine(TiffFolder, fileName));
        List<TiffDirectory> directories = TiffReader.ReadDirectories(data);

        AssertMatchesGolden(fileName, Decode(directories[0], data), ResolveGolden(fileName));
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
        string specificPath = Path.Combine(GoldenFolder, Path.ChangeExtension(fileName, ".png"));

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

        string decodedPath = Path.GetFullPath(Path.Combine(DecodedFolder, Path.ChangeExtension(fileName, ".png")));

        SKImageInfo info = new(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        using SKImage image = SKImage.FromPixelCopy(info, decoded.Pixels);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(decodedPath);
        data.SaveTo(output);

        return decodedPath;
    }

    /// <summary>
    /// Runs the TIFF row decoder over a directory and maps its samples onto straight RGBA the way
    /// ImageMagick flattens them to 8 bits.
    /// </summary>
    private static DecodedImage Decode(TiffDirectory directory, byte[] data)
    {
        TiffRowDecoder decoder = new(directory, data);

        byte[] rowBuffer = new byte[decoder.RowBytes];
        byte[] pixels = new byte[decoder.Width * decoder.Height * BytesPerPixel];
        int[] samples = new int[decoder.ComponentCount];
        int alphaIndex = (directory.ExtraSamples.Length > 0) ? decoder.ComponentCount - directory.ExtraSamples.Length : -1;

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
                WriteRgba(decoder, directory, samples, alphaIndex, pixels.AsSpan(destination, BytesPerPixel));
            }

            row++;
        }

        Assert.Equal(decoder.Height, row);

        return new DecodedImage(decoder.Width, decoder.Height, pixels);
    }

    private static void WriteRgba(TiffRowDecoder decoder, TiffDirectory directory, int[] samples, int alphaIndex, Span<byte> rgba)
    {
        int bits = decoder.BitsPerComponent;
        rgba[3] = (alphaIndex >= 0) ? ToByte(samples[alphaIndex], bits) : byte.MaxValue;

        switch (decoder.Photometric)
        {
            case TiffPhotometric.MinIsBlack:
                {
                    byte gray = ToByte(samples[0], bits);
                    rgba[0] = gray;
                    rgba[1] = gray;
                    rgba[2] = gray;
                    break;
                }
            case TiffPhotometric.MinIsWhite:
                {
                    byte gray = ToByte(((1 << bits) - 1) - samples[0], bits);
                    rgba[0] = gray;
                    rgba[1] = gray;
                    rgba[2] = gray;
                    break;
                }
            case TiffPhotometric.Rgb:
                {
                    rgba[0] = ToByte(samples[0], bits);
                    rgba[1] = ToByte(samples[1], bits);
                    rgba[2] = ToByte(samples[2], bits);
                    break;
                }
            case TiffPhotometric.Palette:
                {
                    ushort[] colorMap = directory.ColorMap ?? throw new InvalidDataException("Palette image has no ColorMap.");
                    int entryCount = 1 << bits;
                    rgba[0] = ToByte(colorMap[samples[0]], 16);
                    rgba[1] = ToByte(colorMap[entryCount + samples[0]], 16);
                    rgba[2] = ToByte(colorMap[(2 * entryCount) + samples[0]], 16);
                    break;
                }
            case TiffPhotometric.Separated:
                {
                    int black = 255 - ToByte(samples[3], bits);
                    rgba[0] = (byte)((((255 - ToByte(samples[0], bits)) * black) + 127) / 255);
                    rgba[1] = (byte)((((255 - ToByte(samples[1], bits)) * black) + 127) / 255);
                    rgba[2] = (byte)((((255 - ToByte(samples[2], bits)) * black) + 127) / 255);
                    break;
                }
            default:
                {
                    throw new NotSupportedException($"Photometric {decoder.Photometric} has no RGBA mapping in the test harness.");
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
    private sealed record DecodedImage(int Width, int Height, byte[] Pixels);
}
