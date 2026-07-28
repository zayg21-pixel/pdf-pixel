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
    /// Largest per-channel difference from the golden image that still counts as a match.
    /// The reversible 5-3 path is exact and the irreversible 9-7 path differs only by the
    /// rounding of its floating-point lifting steps.
    /// </summary>
    private const int MaximumChannelDifference = 2;

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

        (int maximumDifference, double meanDifference, int differingPixels) = Compare(decoded, goldenBitmap);

        _output.WriteLine(
            $"Difference from golden: max {maximumDifference}, mean {meanDifference:F4}, {differingPixels} pixel(s) differ.");

        Assert.True(
            maximumDifference <= MaximumChannelDifference,
            $"File '{fileName}' differs from its golden image by up to {maximumDifference} per channel "
                + $"(allowed: {MaximumChannelDifference}). Inspect {decodedPath} against {goldenPath}.");
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
    /// Compares the decoded samples against the golden image, returning the largest and mean
    /// per-channel difference and how many pixels differ at all.
    /// </summary>
    private static (int MaximumDifference, double MeanDifference, int DifferingPixels) Compare(DecodedImage decoded, SKBitmap golden)
    {
        ReadOnlySpan<byte> goldenPixels = golden.GetPixelSpan();

        int maximumDifference = 0;
        long totalDifference = 0;
        int differingPixels = 0;

        for (int y = 0; y < decoded.Height; y++)
        {
            for (int x = 0; x < decoded.Width; x++)
            {
                int goldenOffset = ((y * golden.Width) + x) * 4;
                int decodedOffset = ((y * decoded.Width) + x) * 3;

                int redDifference = Math.Abs(decoded.Samples[decodedOffset] - goldenPixels[goldenOffset]);
                int greenDifference = Math.Abs(decoded.Samples[decodedOffset + 1] - goldenPixels[goldenOffset + 1]);
                int blueDifference = Math.Abs(decoded.Samples[decodedOffset + 2] - goldenPixels[goldenOffset + 2]);

                int pixelMaximum = Math.Max(redDifference, Math.Max(greenDifference, blueDifference));

                if (pixelMaximum > 0)
                {
                    differingPixels++;
                }

                maximumDifference = Math.Max(maximumDifference, pixelMaximum);
                totalDifference += redDifference + greenDifference + blueDifference;
            }
        }

        double meanDifference = (double)totalDifference / (decoded.Width * decoded.Height * 3);

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
        using SKBitmap bitmap = new(info);

        SKColor[] pixels = new SKColor[decoded.Width * decoded.Height];

        for (int i = 0; i < pixels.Length; i++)
        {
            int offset = i * 3;
            pixels[i] = new SKColor(decoded.Samples[offset], decoded.Samples[offset + 1], decoded.Samples[offset + 2]);
        }

        bitmap.Pixels = pixels;

        using SKImage image = SKImage.FromBitmap(bitmap);
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
        byte[] samples = new byte[converter.Width * converter.Height * 3];

        int row = 0;

        while (converter.TryGetNextRow(rowBuffer) && row < converter.Height)
        {
            for (int x = 0; x < converter.Width; x++)
            {
                int source = x * converter.ComponentCount;
                int destination = ((row * converter.Width) + x) * 3;

                samples[destination] = rowBuffer[source];
                samples[destination + 1] = rowBuffer[source + 1];
                samples[destination + 2] = rowBuffer[source + 2];
            }

            row++;
        }

        return new DecodedImage(converter.Width, converter.Height, samples);
    }

    private sealed record DecodedImage(int Width, int Height, byte[] Samples);
}
