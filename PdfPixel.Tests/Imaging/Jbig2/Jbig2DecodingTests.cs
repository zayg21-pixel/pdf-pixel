using System;
using System.IO;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Jbig2.Model;
using Xunit;
using Xunit.Abstractions;
namespace PdfPixel.Tests.Imaging.Jbig2;

/// <summary>
/// Decoding tests for JBIG2 standalone (.jb2) files.
/// Each test decodes a .jb2 file and compares the result against the reference
/// image (0_bitmap-mmr.jb2) which is known to decode correctly.
/// A minimum of 97% bit similarity is required to pass.
/// </summary>
public class Jbig2DecodingTests
{
    private const double MinimumSimilarity = 0.97;
    private const string Jbig2Folder = "jbig2";
    private const string ReferenceFile = "bitmap-mmr.jb2";

    private readonly ITestOutputHelper _output;
    private readonly ILogger _logger;

    public Jbig2DecodingTests(ITestOutputHelper output)
    {
        _output = output;
        _logger = NullLogger.Instance;
    }

    [Theory(Skip = "Not implemented")]
    [InlineData("bitmap-symbol-context-reuse.jb2")] // not implemented
    [InlineData("bitmap-symbol-symhuffrefine-textrefine.jb2")] // not implemented
    [InlineData("bitmap-symbol-symhuffrefineseveral.jb2")] // not implemented
    public void KnownIssues_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-halftone-skip-dummy.jb2")]
    [InlineData("bitmap-halftone-skip-grid.jb2")]
    [InlineData("bitmap-halftone-skip-grid-template1.jb2")]
    [InlineData("bitmap-halftone-skip-grid-template2.jb2")]
    [InlineData("bitmap-halftone-skip-grid-template3.jb2")]
    public void HalftoneRegion_Skip_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    /// <summary>
    /// Verifies that the reference file (0_bitmap-mmr.jb2) decodes successfully.
    /// </summary>
    [Fact]
    public void Reference_BitmapMmr_DecodesSuccessfully()
    {
        var bitmap = DecodeFile(ReferenceFile);

        Assert.NotNull(bitmap);
        Assert.True(bitmap.Width > 0, "Reference bitmap width should be positive.");
        Assert.True(bitmap.Height > 0, "Reference bitmap height should be positive.");

        _output.WriteLine($"Reference decoded: {bitmap.Width}x{bitmap.Height}");
    }

    [Theory]
    [InlineData("bitmap-template1.jb2")]
    [InlineData("bitmap-template2.jb2")]
    [InlineData("bitmap-template3.jb2")]
    [InlineData("bitmap-tpgdon.jb2")]
    [InlineData("bitmap-template1-tpgdon.jb2")]
    [InlineData("bitmap-template2-tpgdon.jb2")]
    [InlineData("bitmap-template3-tpgdon.jb2")]
    [InlineData("bitmap-customat.jb2")]
    [InlineData("bitmap-customat-tpgdon.jb2")]
    [InlineData("bitmap-template1-customat.jb2")]
    [InlineData("bitmap-template1-customat-tpgdon.jb2")]
    [InlineData("bitmap-template2-customat.jb2")]
    [InlineData("bitmap-template2-customat-tpgdon.jb2")]
    [InlineData("bitmap-template3-customat.jb2")]
    [InlineData("bitmap-template3-customat-tpgdon.jb2")]
    public void GenericRegion_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-symbol.jb2")]
    [InlineData("bitmap-symbol-empty.jb2")]
    [InlineData("bitmap-symbol-negative-sbdsoffset.jb2")]
    [InlineData("bitmap-symbol-textcomposite.jb2")]
    [InlineData("bitmap-symbol-texttopright.jb2")]
    [InlineData("bitmap-symbol-texttoprighttranspose.jb2")]
    [InlineData("bitmap-symbol-textbottomleft.jb2")]
    [InlineData("bitmap-symbol-textbottomlefttranspose.jb2")]
    [InlineData("bitmap-symbol-textbottomright.jb2")]
    [InlineData("bitmap-symbol-textbottomrighttranspose.jb2")]
    [InlineData("bitmap-symbol-texttranspose.jb2")]
    public void SymbolDictionaryAndTextRegionAll_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-symbol-refine.jb2")]
    [InlineData("bitmap-symbol-symbolrefineone.jb2")]
    [InlineData("bitmap-symbol-symbolrefineone-customat.jb2")]
    [InlineData("bitmap-symbol-symbolrefineone-template1.jb2")]
    [InlineData("bitmap-symbol-textrefine.jb2")]
    [InlineData("bitmap-symbol-textrefine-customat.jb2")]
    [InlineData("bitmap-symbol-textrefine-negative-delta-width.jb2")]
    [InlineData("bitmap-symbol-symbolrefineseveral.jb2")]
    [InlineData("bitmap-symbol-symbolrefine-textrefine.jb2")]
    public void SymbolRefinement_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-symbol-symhuff-texthuff.jb2")]
    [InlineData("bitmap-symbol-symhuff-texthuffB10B13.jb2")]
    [InlineData("bitmap-symbol-symhuffB5B3-texthuffB7B9B12.jb2")]
    [InlineData("bitmap-symbol-symhuffrefineone.jb2")]
    [InlineData("bitmap-symbol-symhuffuncompressed-texthuff.jb2")]
    [InlineData("bitmap-symbol-texthuffrefine.jb2")]
    [InlineData("bitmap-symbol-texthuffrefineB15.jb2")]
    [InlineData("bitmap-symbol-texthuffrefinecustom.jb2")]
    [InlineData("bitmap-symbol-texthuffrefinecustomdims.jb2")]
    [InlineData("bitmap-symbol-texthuffrefinecustompos.jb2")]
    [InlineData("bitmap-symbol-texthuffrefinecustomposdims.jb2")]
    [InlineData("bitmap-symbol-texthuffrefinecustomsize.jb2")]
    //[InlineData("bitmap-symbol-symhuffcustom-texthuffcustom.jb2")]
    public void HuffmanCoded_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-refine.jb2")]
    [InlineData("bitmap-refine-customat.jb2")]
    [InlineData("bitmap-refine-customat-tpgron.jb2")]
    [InlineData("bitmap-refine-lossless.jb2")]
    [InlineData("bitmap-refine-page.jb2")]
    [InlineData("bitmap-refine-page-subrect.jb2")]
    [InlineData("bitmap-refine-refine.jb2")]
    [InlineData("bitmap-refine-template1.jb2")]
    [InlineData("bitmap-refine-template1-tpgron.jb2")]
    [InlineData("bitmap-refine-tpgron.jb2")]
    public void RefinementRegion_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-halftone.jb2")]
    [InlineData("bitmap-halftone-template1.jb2")]
    [InlineData("bitmap-halftone-template2.jb2")]
    [InlineData("bitmap-halftone-template3.jb2")]
    [InlineData("bitmap-halftone-10bpp.jb2")]
    [InlineData("bitmap-halftone-10bpp-mmr.jb2")]
    [InlineData("bitmap-halftone-composite.jb2")]
    [InlineData("bitmap-halftone-grid.jb2")]
    [InlineData("bitmap-halftone-refine.jb2")]
    public void HalftoneRegion_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-composite-and-xnor.jb2")]
    [InlineData("bitmap-composite-and-xnor-halftone.jb2")]
    [InlineData("bitmap-composite-and-xnor-refine.jb2")]
    [InlineData("bitmap-composite-and-xnor-text.jb2")]
    [InlineData("bitmap-composite-or-xor-replace.jb2")]
    [InlineData("bitmap-composite-or-xor-replace-halftone.jb2")]
    [InlineData("bitmap-composite-or-xor-replace-refine.jb2")]
    [InlineData("bitmap-composite-or-xor-replace-text.jb2")]
    public void CompositeOperators_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-stripe.jb2")]
    [InlineData("bitmap-stripe-single.jb2")]
    [InlineData("bitmap-stripe-single-no-end-of-stripe.jb2")]
    [InlineData("bitmap-stripe-last-implicit.jb2")]
    [InlineData("bitmap-stripe-initially-unknown-height.jb2")]
    public void Stripe_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    [Theory]
    [InlineData("bitmap-randomaccess.jb2")]
    [InlineData("bitmap-initially-unknown-size.jb2")]
    [InlineData("bitmap-p32-eof.jb2")]
    [InlineData("bitmap-trailing-7fff-stripped.jb2")]
    [InlineData("bitmap-trailing-7fff-stripped-harder.jb2")]
    [InlineData("bitmap-trailing-7fff-stripped-harder-refine.jb2")]
    [InlineData("bitmap-symbol-big-segmentid.jb2")]
    public void Miscellaneous_DecodesWithSufficientSimilarity(string fileName)
    {
        AssertDecodesWithSimilarityToReference(fileName);
    }

    private void AssertDecodesWithSimilarityToReference(string fileName)
    {
        var referenceBitmap = DecodeFile(ReferenceFile);
        Assert.NotNull(referenceBitmap);

        var testBitmap = DecodeFile(fileName);
        Assert.NotNull(testBitmap);

        _output.WriteLine($"Reference: {referenceBitmap.Width}x{referenceBitmap.Height}, Test ({fileName}): {testBitmap.Width}x{testBitmap.Height}");

        double similarity = ComputeBitSimilarity(referenceBitmap, testBitmap);
        _output.WriteLine($"Bit similarity: {similarity:P2}");

        Assert.True(
            similarity >= MinimumSimilarity,
            $"File '{fileName}' decoded with only {similarity:P2} bit similarity to reference (minimum: {MinimumSimilarity:P0}).");
    }

    private Jbig2Bitmap DecodeFile(string fileName)
    {
        string filePath = Path.Combine(Jbig2Folder, fileName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"JBIG2 test file not found: {filePath}");
        }

        byte[] data = File.ReadAllBytes(filePath);
        var decoder = new Jbig2PageDecoder();

        // For standalone .jb2 files, pass as page data with no globals
        return decoder.Decode(data, 0, 0);
    }

    /// <summary>
    /// Computes the fraction of bits that match between two bitmaps.
    /// If dimensions differ, compares the overlapping area and counts
    /// non-overlapping pixels as mismatches.
    /// </summary>
    private static double ComputeBitSimilarity(Jbig2Bitmap reference, Jbig2Bitmap test)
    {
        int maxWidth = Math.Max(reference.Width, test.Width);
        int maxHeight = Math.Max(reference.Height, test.Height);

        if (maxWidth == 0 || maxHeight == 0)
        {
            return 1.0;
        }

        long totalBits = (long)maxWidth * maxHeight;
        long mismatchBits = 0;

        int overlapHeight = Math.Min(reference.Height, test.Height);
        int overlapBytes = Math.Min(reference.Width, test.Width) >> 3;

        for (int y = 0; y < overlapHeight; y++)
        {
            var refRow = reference.GetRowReadOnly(y);
            var testRow = test.GetRowReadOnly(y);

            for (int b = 0; b < overlapBytes; b++)
            {
                mismatchBits += BitOperations.PopCount((uint)(refRow[b] ^ testRow[b]));
            }
        }

        // Non-overlapping area counts as all mismatches
        long overlapArea = (long)(overlapBytes * 8) * overlapHeight;
        long nonOverlapBits = totalBits - overlapArea;

        return (double)(totalBits - mismatchBits - nonOverlapBits) / totalBits;
    }
}
