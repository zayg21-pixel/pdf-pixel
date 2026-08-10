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
/// Decoding tests for JPEG 2000 codestreams. The <c>baboon-*</c> files in <c>jpx/</c> each encode
/// the same source image with one coding feature varied, so a failure names the feature that
/// broke; the remaining files are codestreams taken from PDFs that exposed a bug.
/// Each decode is compared against a golden PNG in <c>jpx/golden/</c> produced by
/// OpenJPEG's opj_decompress, and is written to <c>jpx/decoded/</c> in the test output
/// directory so it can be inspected by eye afterwards.
/// </summary>
public class JpxDecodingTests
{
    // The corpus is generated with OpenJPEG's opj_compress, which cannot produce every feature
    // a decoder has to handle. These axes are therefore uncovered, and the gap is in the corpus
    // rather than in the code:
    //
    // TODO: [HIGH] Cover a COC that differs from the COD. opj_compress has no per-component
    // coding style option, so it never writes one, and the COC segments issue12752.jp2 carries
    // only repeat the COD's values. PdfPixel parses COC and applies none of it, so a component
    // given its own transform, decomposition levels, code-block size or precinct sizes decodes
    // with the main header's.
    // TODO: [HIGH] Cover POC. opj_compress accepts -POC but writes no marker for any spec tried,
    // though the library itself has opj_j2k_write_poc.
    // TODO: [HIGH] Cover PPM and PPT. opj_compress has no flag for packed packet headers.
    // TODO: [MEDIUM] Cover RGN. opj_compress -ROI produces a codestream that opj_decompress
    // then fails to decode, so no golden image can be produced from it.
    // TODO: [MEDIUM] Cover the pclr and cmap boxes. opj_compress writes no palette.
    //
    // The ITU conformance codestreams exercise all of the above and are what OpenJPEG's own test
    // suite runs against. They are not in the openjpeg clone: tests/conformance holds the harness
    // and the files live in the separate openjpeg-data repository. Cloning it is the single step
    // that unblocks every entry above.
    //
    // TODO: [MEDIUM] Cover component precisions other than 8 bits, grayscale and 4-component
    // images, and signed samples. opj_compress generates all of these from raw input via -F, so
    // only the harness is missing: Decode below rejects anything that is not 8-bit RGB, and the
    // comparison assumes one byte per channel.

    private const string JpxFolder = "jpx";
    private const string GoldenFolder = "jpx/golden";
    private const string DecodedFolder = "jpx/decoded";

    /// <summary>
    /// Bytes per pixel of the buffers compared, which hold straight RGBA.
    /// </summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    /// Per-channel difference allowed for the irreversible 9-7 transform, whose floating-point
    /// lifting steps leave the last step or two of each sample to the implementation's rounding.
    /// </summary>
    private const int IrreversibleChannelTolerance = 2;

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
    [InlineData("baboon-tileparts-l.j2k")]
    [InlineData("baboon-tileparts-c.j2k")]
    public void TileGrid_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// An image or tile origin away from zero leaves the partitions below the tile, which are all
    /// anchored at the reference grid's origin rather than the tile's, starting part way into
    /// their first cell.
    /// </summary>
    [Theory(Skip = "Needs the reference grid origin: the image spans Xsiz - XOsiz by Ysiz - YOsiz, "
        + "and a tile starts at XTOsiz + index * XTsiz. See the TODO in JpxTileToRowConverter.")]
    [InlineData("baboon-offset-image.j2k")]
    [InlineData("baboon-offset-tile.j2k")]
    public void ReferenceGridOffsets_DecodeCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// The length markers carry no coding information, so a decoder has to step over them
    /// without letting them disturb what follows.
    /// </summary>
    [Theory]
    [InlineData("baboon-markers-tlm-plt.j2k")]
    public void InformationalMarkers_DecodeCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

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
    /// A QCC segment gives one component its own step sizes, which the reversible path turns into
    /// a different magnitude alignment than the main header's QCD would. Taking the QCD for such a
    /// component reconstructs its coefficients off by a factor of two per exponent it differs by.
    /// </summary>
    [Theory]
    [InlineData("issue12752.jp2")]
    public void ComponentQuantization_DecodesCloseToGolden(string fileName) => AssertDecodesCloseToGolden(fileName);

    /// <summary>
    /// The irreversible 9-7 transform, the other of the two wavelets ITU-T T.800 defines. It
    /// reconstructs through floating-point lifting steps rather than the exact integer ones of
    /// the reversible 5-3, so its samples are compared with a tolerance.
    /// </summary>
    [Theory]
    [InlineData("baboon-irreversible.j2k")]
    [InlineData("baboon-irreversible-tiles.j2k")]
    [InlineData("baboon-irreversible-layers.j2k")]
    [InlineData("baboon-irreversible-precincts.j2k")]
    public void IrreversibleTransform_DecodesCloseToGolden(string fileName)
        => AssertDecodesCloseToGolden(fileName, IrreversibleChannelTolerance);

    /// <summary>
    /// A subsampled component covers the reference grid in steps larger than one sample, so it
    /// carries fewer samples than the image has pixels and has to be stretched back over the
    /// grid on output. The uniform case subsamples every component alike, which makes the
    /// reference grid larger than any component; the chroma case leaves the first component at
    /// full rate and halves the other two.
    /// </summary>
    [Theory(Skip = "Needs component subsampling: every grid below the tile derives from the "
        + "tile-component bounds tcx0 = ceil(tx0 / XRsiz), and the samples have to be stretched "
        + "back over the reference grid on output. See the TODO in JpxBandGeometry.")]
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

    /// <summary>
    /// Decodes a corpus file, writes it out for inspection, and requires it to reproduce its
    /// golden image sample for sample.
    /// </summary>
    /// <param name="fileName">Corpus file to decode.</param>
    /// <param name="maximumChannelDifference">
    /// Largest per-channel difference to accept. Zero for the reversible 5-3 transform, which is
    /// exact; the irreversible 9-7 transform reconstructs through floating-point lifting steps,
    /// so its samples land within a step or two of another decoder's rounding.
    /// </param>
    private void AssertDecodesCloseToGolden(string fileName, int maximumChannelDifference = 0)
    {
        DecodedImage decoded = Decode(fileName);

        string decodedPath = SaveDecoded(fileName, decoded);
        _output.WriteLine($"Decoded {decoded.Width}x{decoded.Height} to {decodedPath}");

        string goldenPath = ResolveGolden(fileName);

        using SKBitmap goldenBitmap = LoadGolden(goldenPath);

        Assert.Equal(goldenBitmap.Width, decoded.Width);
        Assert.Equal(goldenBitmap.Height, decoded.Height);

        if (maximumChannelDifference == 0 && Matches(decoded, goldenBitmap))
        {
            return;
        }

        // Only a decode that is not already known to match pays for measuring how far off it is.
        (int maximumDifference, double meanDifference, int differingPixels) = Measure(decoded, goldenBitmap);

        _output.WriteLine(
            $"Difference from golden: max {maximumDifference}, mean {meanDifference:F4}, {differingPixels} pixel(s) differ.");

        Assert.True(
            maximumDifference <= maximumChannelDifference,
            $"File '{fileName}' differs from its golden image by up to {maximumDifference} per channel "
                + $"(allowed: {maximumChannelDifference}), mean {meanDifference:F4} over {differingPixels} pixel(s). "
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

        // TODO: [MEDIUM] Accept grayscale, four-component and non-8-bit images so the corpus can
        // cover them. Each needs mapping onto the RGBA buffer the comparison and the PNG snapshot
        // use: a single component replicated across the three channels, the fourth component
        // dropped or carried as alpha, and precisions above or below 8 bits scaled to a byte the
        // same way the golden PNG's own precision is scaled.
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
