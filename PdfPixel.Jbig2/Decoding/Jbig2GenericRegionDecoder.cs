using PdfPixel.Jbig2.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Decodes generic regions using arithmetic or MMR coding (ITU-T T.88 Section 6.2).
/// </summary>
internal static class Jbig2GenericRegionDecoder
{
    /// <summary>
    /// Decodes a generic region bitmap where <paramref name="data"/> begins with the flags byte
    /// (ITU-T T.88 Section 7.4.6.2) followed by the coded bitstream.
    /// Use this overload for generic region segments where the flags byte is part of the stream.
    /// </summary>
    /// <param name="data">Flags byte followed by encoded data.</param>
    /// <param name="regionHeader">Region header carrying dimensions and placement metadata.</param>
    /// <param name="observer">Execution observer for long-running operations.</param>
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap Decode(in ReadOnlySpan<byte> data, Jbig2RegionHeader regionHeader, IJBig2ExectionObserver? observer = null)
    {
        if (data.IsEmpty)
        {
            throw new InvalidOperationException("JBIG2 generic region data too short: missing flags byte.");
        }

        Jbig2GenericRegionFlags flags = new(data[0]);
        return Decode(flags, data.Slice(1), regionHeader.Width, regionHeader.Height, observer);
    }

    /// <summary>
    /// Decodes a generic region bitmap using pre-parsed flags and a coded data slice.
    /// Use this overload when the flags byte originates from a different header (e.g. pattern dictionary,
    /// halftone region) and the coded data is already separated from it.
    /// </summary>
    /// <param name="flags">Pre-parsed generic region flags.</param>
    /// <param name="codedData">Encoded data (AT bytes + bitstream for arithmetic; raw bitstream for MMR).</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <param name="observer">Execution observer for long-running operations.</param>
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap Decode(in Jbig2GenericRegionFlags flags, in ReadOnlySpan<byte> codedData, int width, int height, IJBig2ExectionObserver? observer = null)
    {
        if (flags.UseMmr)
        {
            return Jbig2MmrDecoder.Decode(codedData, width, height, out _, observer);
        }

        int atCount = Jbig2Templates.AtPixelCount(flags.TemplateId);
        Jbig2AtPixels atPixels = Jbig2Templates.ReadAtPixelPairs(codedData, atCount);
        int offset = atCount * 2;

        return DecodeArithmeticWithAt(flags.TemplateId, flags.TypicalPrediction, codedData.Slice(offset), width, height, atPixels, observer);
    }

    /// <summary>
    /// Decodes a generic region bitmap with explicitly supplied AT pixels.
    /// Use this overload when AT pixel values are known from the segment header rather than
    /// being embedded at the front of the coded data (e.g. pattern dictionary segments,
    /// ITU-T T.88 Section 7.4.4.1.2).
    /// </summary>
    /// <param name="flags">Pre-parsed generic region flags.</param>
    /// <param name="codedData">Encoded bitstream (no leading AT bytes).</param>
    /// <param name="width">Region width in pixels.</param>
    /// <param name="height">Region height in pixels.</param>
    /// <param name="atPixels">Adaptive template pixel offsets.</param>
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap DecodeWithAt(
        in Jbig2GenericRegionFlags flags,
        in ReadOnlySpan<byte> codedData,
        int width,
        int height,
        in Jbig2AtPixels atPixels)
    {
        if (flags.UseMmr)
        {
            return Jbig2MmrDecoder.Decode(codedData, width, height, out _);
        }

        return DecodeArithmeticWithAt(flags.TemplateId, flags.TypicalPrediction, codedData, width, height, atPixels);
    }

    /// <summary>
    /// Core arithmetic generic region decode using the supplied AT pixel values and coded data.
    /// </summary>
    private static Jbig2Bitmap DecodeArithmeticWithAt(
        int templateId,
        bool typicalPrediction,
        in ReadOnlySpan<byte> codedData,
        int width,
        int height,
        in Jbig2AtPixels atPixels,
        IJBig2ExectionObserver? observer = null)
    {
        Jbig2Bitmap bitmap = new(width, height);
        scoped var decoder = new Jbig2ArithmeticReader(codedData);

        int contextSize = Jbig2Templates.GetContextSize(templateId);
        scoped Span<byte> contexts = stackalloc byte[contextSize];
        var ltp = false;

        Jbig2RowTemplate fastTemplate = Jbig2Templates.BuildFastTemplate(templateId, atPixels);

        // Pseudo-pixel context for TPGDON (ITU-T T.88 Table 6)
        int pseudoPixelContext = templateId switch
        {
            0 => 0x9b25,
            1 => 0x0795,
            2 => 0x00e5,
            3 => 0x0195,
            _ => 0x9b25
        };

        for (int y = 0; y < height; y++)
        {
            observer?.Notify();

            if (typicalPrediction)
            {
                int tpBit = decoder.DecodeBit(ref contexts[pseudoPixelContext]);
                ltp ^= (tpBit == 1);

                if (ltp)
                {
                    if (y > 0)
                    {
                        bitmap.GetRow(y - 1).CopyTo(bitmap.GetRow(y));
                    }

                    continue;
                }
            }

            Jbig2RowDecoder.DecodeRow(ref decoder, bitmap, contexts, y, width, fastTemplate);
        }

        return bitmap;
    }
}
