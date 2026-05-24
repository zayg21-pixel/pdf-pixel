using System;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Decodes JBIG2 generic refinement region segments (ITU-T T.88 Section 6.3).
/// Refinement regions refine a reference bitmap (from a previous decode) using
/// template-based arithmetic coding with pixels from both the current and reference bitmaps.
/// Context label layout follows the specification (LSB-first, ITU-T T.88 Tables 12A/12B).
/// </summary>
internal static class Jbig2RefinementRegionDecoder
{
    /// <summary>
    /// Decodes a refinement region where <paramref name="data"/> begins with the flags byte
    /// (ITU-T T.88 Section 7.4.7.2), followed by optional AT pixel bytes, then the coded bitstream.
    /// Use this overload for standalone refinement region segments.
    /// </summary>
    /// <param name="data">Flags byte, optional AT pixels, then encoded refinement data.</param>
    /// <param name="regionHeader">Region header carrying dimensions and placement metadata.</param>
    /// <param name="reference">Reference bitmap to refine.</param>
    /// <param name="referenceOffsetX">X offset of reference bitmap relative to output.</param>
    /// <param name="referenceOffsetY">Y offset of reference bitmap relative to output.</param>
    /// <returns>Refined bitmap.</returns>
    internal static Jbig2Bitmap Decode(
        ReadOnlySpan<byte> data,
        Jbig2RegionHeader regionHeader,
        Jbig2Bitmap reference,
        int referenceOffsetX = 0,
        int referenceOffsetY = 0)
    {
        if (data.IsEmpty)
        {
            throw new InvalidOperationException("JBIG2 refinement region data too short: missing flags byte.");
        }

        var flags = new Jbig2RefinementRegionFlags(data[0]);
        int atCount = flags.AtPixelCount;
        Jbig2AtPixels atPixels = Jbig2Templates.ReadAtPixelPairs(data.Slice(1), atCount);
        int offset = 1 + atCount * 2;

        if (reference == null)
        {
            return new Jbig2Bitmap(regionHeader.Width, regionHeader.Height);
        }

        var bitmap = new Jbig2Bitmap(regionHeader.Width, regionHeader.Height);
        var decoder = new Jbig2ArithmeticReader(data.Slice(offset));

        int contextSize = flags.TemplateId == 0 ? 1 << 13 : 1 << 10;
        Span<byte> contexts = new byte[contextSize];

        DecodeRegion(
            ref decoder,
            bitmap,
            contexts,
            regionHeader.Width,
            regionHeader.Height,
            flags.TemplateId,
            reference,
            referenceOffsetX,
            referenceOffsetY,
            atPixels.AtX,
            atPixels.AtY,
            flags.TypicalPrediction);

        return bitmap;
    }

    /// <summary>
    /// Decodes a refinement bitmap using an existing arithmetic decoder and a shared context.
    /// Used for inline refinement within text regions and symbol dictionaries.
    /// </summary>
    /// <param name="decoder">The active arithmetic decoder.</param>
    /// <param name="context">Arithmetic context owning the GR array, refinement template, and AT offsets.</param>
    /// <param name="width">Output bitmap width.</param>
    /// <param name="height">Output bitmap height.</param>
    /// <param name="reference">Reference bitmap to refine.</param>
    /// <param name="referenceOffsetX">X offset of reference within the output bitmap.</param>
    /// <param name="referenceOffsetY">Y offset of reference within the output bitmap.</param>
    internal static Jbig2Bitmap DecodeInline(
        ref Jbig2ArithmeticReader decoder,
        Jbig2ArithmeticContext context,
        int width,
        int height,
        Jbig2Bitmap reference,
        int referenceOffsetX,
        int referenceOffsetY)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        Span<byte> contexts = context.Gr;

        DecodeRegion(
            ref decoder,
            bitmap,
            contexts,
            width,
            height,
            context.RefinementTemplate,
            reference,
            referenceOffsetX,
            referenceOffsetY,
            context.RefinementAtX,
            context.RefinementAtY,
            prediction: false);

        return bitmap;
    }

    /// <summary>
    /// Core refinement region decode loop, shared by standalone and inline paths.
    /// Implements ITU-T T.88 Section 6.3.5 with optional TPGRON (Section 6.3.5.6).
    /// </summary>
    private static void DecodeRegion(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        Span<byte> contexts,
        int width,
        int height,
        int templateId,
        Jbig2Bitmap reference,
        int refDx,
        int refDy,
        sbyte[] atX,
        sbyte[] atY,
        bool prediction)
    {
        // TPGRON start context (ITU-T T.88 Table 13):
        // Context with only the reference center pixel (rx, ry) bit set:
        // template 0 → bit 8 = 0x0100, template 1 → bit 7 = 0x0080
        int tpgronContext = templateId == 0 ? 0x0100 : 0x0080;
        int ltp = 0;

        var fastTemplate = Jbig2Templates.BuildRefinementTemplate(templateId, atX, atY);

        for (int y = 0; y < height; y++)
        {
            if (prediction)
            {
                int sltp = decoder.DecodeBit(ref contexts[tpgronContext]);
                ltp ^= sltp;
            }

            Jbig2RowDecoder.DecodeRefinementRow(
                ref decoder, bitmap, contexts, y, width, fastTemplate,
                reference, refDx, refDy, usePrediction: ltp != 0);
        }
    }
}
