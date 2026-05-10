using PdfPixel.Imaging.Jbig2.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Jbig2.Decoding;

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
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap Decode(ReadOnlySpan<byte> data, Jbig2RegionHeader regionHeader)
    {
        if (data.IsEmpty)
        {
            throw new InvalidOperationException("JBIG2 generic region data too short: missing flags byte.");
        }

        var flags = new Jbig2GenericRegionFlags(data[0]);
        return Decode(flags, data.Slice(1), regionHeader.Width, regionHeader.Height);
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
    /// <returns>Decoded bitmap.</returns>
    internal static Jbig2Bitmap Decode(Jbig2GenericRegionFlags flags, ReadOnlySpan<byte> codedData, int width, int height)
    {
        if (flags.UseMmr)
        {
            return Jbig2MmrDecoder.Decode(codedData, width, height);
        }

        int atCount = flags.TemplateId == 0 ? 4 : 1;
        var atX = new sbyte[atCount];
        var atY = new sbyte[atCount];
        int offset = 0;

        for (int i = 0; i < atCount; i++)
        {
            if (offset + 1 < codedData.Length)
            {
                atX[i] = (sbyte)codedData[offset];
                atY[i] = (sbyte)codedData[offset + 1];
                offset += 2;
            }
        }// TOOD: this is AGAIN repeated

        var atPixels = new Jbig2AtPixels(atX, atY);

        return DecodeArithmeticWithAt(flags.TemplateId, flags.TypicalPrediction, codedData.Slice(offset), width, height, atPixels);
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
        Jbig2GenericRegionFlags flags,
        ReadOnlySpan<byte> codedData,
        int width,
        int height,
        Jbig2AtPixels atPixels)
    {
        if (flags.UseMmr)
        {
            return Jbig2MmrDecoder.Decode(codedData, width, height);
        }

        return DecodeArithmeticWithAt(flags.TemplateId, flags.TypicalPrediction, codedData, width, height, atPixels);
    }

    /// <summary>
    /// Core arithmetic generic region decode using the supplied AT pixel values and coded data.
    /// </summary>
    private static Jbig2Bitmap DecodeArithmeticWithAt(
        int templateId,
        bool typicalPrediction,
        ReadOnlySpan<byte> codedData,
        int width,
        int height,
        Jbig2AtPixels atPixels)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        scoped var decoder = new Jbig2ArithmeticReader(codedData);

        int contextSize = Jbig2Templates.GetContextSize(templateId);
        scoped Span<byte> contexts = stackalloc byte[contextSize];
        bool ltp = false;

        var fastTemplate = Jbig2Templates.BuildFastTemplate(templateId, atPixels);

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
