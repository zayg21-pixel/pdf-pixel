using System;
using System.IO;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using SkiaSharp;
using Xunit;
using Xunit.Abstractions;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Decoding tests for JPEG 2000 codestreams. Every file in <c>jpx/</c> encodes the same
/// source image with one coding feature varied, so a failure names the feature that broke.
/// Each decode is compared against a golden PNG in <c>jpx/golden/</c> produced by
/// OpenJPEG's opj_decompress, and is written to <c>jpx/decoded/</c> in the test output
/// directory so it can be inspected by eye afterwards.
/// </summary>
public class JpxDecodingTests
{
    private const string JpxFolder = "jpx";
    private const string GoldenFolder = "jpx/golden";
    private const string DecodedFolder = "jpx/decoded";

    /// <summary>
    /// Bytes per pixel of the buffers compared, which hold straight RGBA.
    /// </summary>
    private const int BytesPerPixel = 4;

    private readonly ITestOutputHelper _output;

    public JpxDecodingTests(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// The tile grid drives subband, precinct and code-block geometry, and a ragged grid
    /// leaves partial tiles along the right and bottom edges.
    /// </summary>
    [Theory]
    [InlineData("baboon-default.j2k")]
    [InlineData("baboon-tiles-256.j2k")]
    [InlineData("baboon-tiles-ragged.j2k")]
    [InlineData("baboon-tileparts-r.j2k")]
    public void TileGrid_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// The number of decomposition levels sets how many times the inverse DWT runs.
    /// </summary>
    [Theory]
    [InlineData("baboon-levels-0.j2k")]
    [InlineData("baboon-levels-8.j2k")]
    public void DecompositionLevels_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// Each progression order has its own packet parser, and all of them must enumerate
    /// the same packets in the order the encoder wrote them.
    /// </summary>
    [Theory]
    [InlineData("baboon-prog-rlcp.j2k")]
    [InlineData("baboon-prog-rpcl.j2k")]
    [InlineData("baboon-prog-pcrl.j2k")]
    [InlineData("baboon-prog-cprl.j2k")]
    public void ProgressionOrder_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// Multiple quality layers make a code-block contribute to several packets, which is
    /// the only case where a code-block accumulates data across layers.
    /// </summary>
    [Theory]
    [InlineData("baboon-layers-5.j2k")]
    public void QualityLayers_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// Precincts subdivide a resolution level, and the SOP and EPH markers add
    /// synchronisation bytes the packet parser has to step over.
    /// </summary>
    [Theory]
    [InlineData("baboon-precincts-eph.j2k")]
    [InlineData("baboon-sop-eph.j2k")]
    public void PrecinctsAndMarkers_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// A resolution split into several precincts is the only case where the progression
    /// orders visit packets in genuinely different sequences, since a single precinct per
    /// resolution makes every order degenerate to the same one.
    /// </summary>
    [Theory]
    [InlineData("baboon-precincts-rlcp.j2k")]
    [InlineData("baboon-precincts-rpcl.j2k")]
    [InlineData("baboon-precincts-pcrl.j2k")]
    [InlineData("baboon-precincts-cprl.j2k")]
    [InlineData("baboon-precincts-layers.j2k")]
    public void PrecinctsPerProgressionOrder_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// Small code-blocks multiply the number of Tier-1 invocations and shrink the stripes
    /// the coding passes walk.
    /// </summary>
    [Theory]
    [InlineData("baboon-cblk-16.j2k")]
    [InlineData("baboon-cblk-4.j2k")]
    public void CodeBlockSize_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// Without the multiple component transform each component is coded independently.
    /// </summary>
    [Theory]
    [InlineData("baboon-mct-off.j2k")]
    public void ComponentTransform_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// A subsampled component covers the reference grid in steps larger than one sample, so it
    /// carries fewer samples than the image has pixels and has to be stretched back over the
    /// grid on output. The uniform case subsamples every component alike, which makes the
    /// reference grid larger than any component; the chroma case leaves the first component at
    /// full rate and halves the other two.
    /// </summary>
    [Theory]
    [InlineData("baboon-subsample-uniform.j2k")]
    [InlineData("baboon-subsample-chroma.j2k")]
    public void SubsampledComponents_DecodeCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// The COD code-block style bits change how the arithmetic coder is driven within a
    /// code-block.
    /// </summary>
    [Theory]
    [InlineData("baboon-mode-bypass.j2k")]
    [InlineData("baboon-mode-reset.j2k")]
    [InlineData("baboon-mode-termall.j2k")]
    [InlineData("baboon-mode-vcausal.j2k")]
    [InlineData("baboon-mode-predterm.j2k")]
    [InlineData("baboon-mode-segsym.j2k")]
    [InlineData("baboon-mode-all.j2k")]
    public void CodeBlockStyle_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    private void AssertDecodesCloseToGolden(string fileName)
    {
        DecodedImage decoded = Decode(fileName);

        string decodedPath = SaveDecoded(fileName, decoded);
        _output.WriteLine($"Decoded {decoded.Width}x{decoded.Height} to {decodedPath}");

        string goldenPath = ResolveGolden(fileName);

        using SKBitmap goldenBitmap = LoadGolden(goldenPath);

        Assert.Equal(goldenBitmap.Width, decoded.Width);
        Assert.Equal(goldenBitmap.Height, decoded.Height);

        if (Matches(decoded, goldenBitmap))
        {
            return;
        }

        // Only a mismatch pays for measuring how far off the samples are.
        (int maximumDifference, double meanDifference, int differingPixels) = Measure(decoded, goldenBitmap);

        Assert.Fail(
            $"File '{fileName}' does not decode to its golden image: up to {maximumDifference} per channel, "
                + $"mean {meanDifference:F4} over {differingPixels} differing pixel(s). "
                + $"Inspect {decodedPath} against {goldenPath}.");
    }

    /// <summary>
    /// Whether every sample of the decode equals the golden image. Both buffers hold straight
    /// RGBA, so the rows compare directly.
    /// </summary>
    private static bool Matches(DecodedImage decoded, SKBitmap golden)
    {
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();
        ReadOnlySpan<byte> decodedPixels = decoded.Pixels;
        int rowBytes = decoded.Width * BytesPerPixel;

        for (int y = 0; y < decoded.Height; y++)
        {
            ReadOnlySpan<byte> goldenRow = goldenPixels.Slice(y * golden.RowBytes, rowBytes);
            ReadOnlySpan<byte> decodedRow = decodedPixels.Slice(y * rowBytes, rowBytes);

            if (!decodedRow.SequenceEqual(goldenRow))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the golden image a corpus file is expected to decode to. Coding options that
    /// only change how the samples are carried share one golden image named after the source;
    /// a file that is expected to decode to something else gets its own golden image named
    /// after the file.
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
    /// Measures how far a mismatching decode is from the golden image, for the failure message.
    /// The alpha byte is left out, since every sample it compares is opaque.
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
                int goldenOffset = goldenRow + (x * BytesPerPixel);
                int decodedOffset = decodedRow + (x * BytesPerPixel);

                int redDifference = Math.Abs(decodedPixels[decodedOffset] - goldenPixels[goldenOffset]);
                int greenDifference = Math.Abs(decodedPixels[decodedOffset + 1] - goldenPixels[goldenOffset + 1]);
                int blueDifference = Math.Abs(decodedPixels[decodedOffset + 2] - goldenPixels[goldenOffset + 2]);

                int pixelMaximum = Math.Max(redDifference, Math.Max(greenDifference, blueDifference));

                if (pixelMaximum > 0)
                {
                    differingPixels++;
                }

                maximumDifference = Math.Max(maximumDifference, pixelMaximum);
                totalDifference += redDifference + greenDifference + blueDifference;
            }
        }

        double meanDifference = (double)totalDifference / ((long)decoded.Width * decoded.Height * 3);

        return (maximumDifference, meanDifference, differingPixels);
    }

    /// <summary>
    /// Decodes the golden PNG into straight RGBA bytes so its samples can be read directly.
    /// </summary>
    private static SKBitmap LoadGolden(string goldenPath)
    {
        using SKBitmap bitmap = SKBitmap.Decode(goldenPath);

        if (bitmap == null)
        {
            throw new InvalidDataException($"Golden image could not be decoded: {goldenPath}");
        }

        SKImageInfo info = new(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        SKBitmap converted = new(info);

        if (!bitmap.CopyTo(converted, SKColorType.Rgba8888))
        {
            converted.Dispose();
            throw new InvalidDataException($"Golden image could not be converted to RGBA: {goldenPath}");
        }

        return converted;
    }

    /// <summary>
    /// Writes the decoded samples out as a PNG so a failing comparison can be examined by eye.
    /// </summary>
    private static string SaveDecoded(string fileName, DecodedImage decoded)
    {
        Directory.CreateDirectory(DecodedFolder);

        string decodedPath = Path.GetFullPath(Path.Combine(DecodedFolder, Path.ChangeExtension(fileName, ".png")));

        SKImageInfo info = new(decoded.Width, decoded.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);

        using SKImage image = SKImage.FromPixelCopy(info, decoded.Pixels);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream output = File.Create(decodedPath);
        data.SaveTo(output);

        return decodedPath;
    }

    /// <summary>
    /// Runs the JPX pipeline over a corpus file and collects its rows into packed RGB bytes.
    /// </summary>
    private static DecodedImage Decode(string fileName)
    {
        string filePath = Path.Combine(JpxFolder, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JPX test file not found: {filePath}");
        }

        byte[] data = File.ReadAllBytes(filePath);
        JpxHeader header = JpxReader.ParseHeader(data);
        JpxTileProvider provider = new(header, data.AsSpan(header.CodestreamOffset));
        JpxTileToRowConverter converter = new(header, provider);

        if (converter.BitsPerComponent != 8 || converter.ColorComponentCount != 3)
        {
            throw new NotSupportedException(
                $"'{fileName}' decodes to {converter.ColorComponentCount} component(s) at "
                    + $"{converter.BitsPerComponent} bits, and the comparison expects 8-bit RGB.");
        }

        int rowBytes = ((converter.Width * converter.ComponentCount * converter.BitsPerComponent) + 7) / 8;
        byte[] rowBuffer = new byte[rowBytes];
        byte[] pixels = new byte[converter.Width * converter.Height * BytesPerPixel];

        int row = 0;

        while (converter.TryGetNextRow(rowBuffer) && row < converter.Height)
        {
            for (int x = 0; x < converter.Width; x++)
            {
                int source = x * converter.ComponentCount;
                int destination = ((row * converter.Width) + x) * BytesPerPixel;

                pixels[destination] = rowBuffer[source];
                pixels[destination + 1] = rowBuffer[source + 1];
                pixels[destination + 2] = rowBuffer[source + 2];
                pixels[destination + 3] = byte.MaxValue;
            }

            row++;
        }

        return new DecodedImage(converter.Width, converter.Height, pixels);
    }

    /// <summary>
    /// A decoded corpus image as straight RGBA, the form both the comparison and the PNG
    /// snapshot consume.
    /// </summary>
    private sealed record DecodedImage(int Width, int Height, byte[] Pixels);
}
