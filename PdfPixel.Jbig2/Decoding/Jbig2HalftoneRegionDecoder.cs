using System;
using System.Buffers.Binary;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Decodes JBIG2 halftone region segments (ITU-T T.88 Section 6.6).
/// Halftone regions use patterns from a pattern dictionary to render grayscale images
/// as dithered bi-level bitmaps using a grid of pattern cells.
/// </summary>
internal static class Jbig2HalftoneRegionDecoder
{
    /// <summary>
    /// Decodes a halftone region using the given patterns.
    /// </summary>
    /// <param name="segmentData">Encoded halftone region data (after the region info).</param>
    /// <param name="regionInfo">Region dimensions and position.</param>
    /// <param name="patterns">Available pattern bitmaps from the referred pattern dictionary.</param>
    /// <returns>Decoded region bitmap.</returns>
    public static Jbig2Bitmap Decode(
        in ReadOnlySpan<byte> segmentData,
        Jbig2RegionHeader regionInfo,
        Jbig2Bitmap[] patterns)
    {
        if (segmentData.Length < 21 || patterns == null || patterns.Length == 0)
        {
            return new Jbig2Bitmap(regionInfo.Width, regionInfo.Height);
        }

        // Parse halftone region flags (ITU-T T.88 Section 7.4.5.1.1)
        byte flags = segmentData[0];
        bool mmr = (flags & 0x01) != 0;
        int templateId = (flags >> 1) & 0x03;
        bool enableSkip = (flags & 0x08) != 0;

        var combinationOp = (Jbig2CombinationOperator)((flags >> 4) & 0x07);
        var defaultPixel = (byte)((flags >> 7) & 1);

        // Grid dimensions
        int gridWidth = BinaryPrimitives.ReadInt32BigEndian(segmentData.Slice(1, 4));
        int gridHeight = BinaryPrimitives.ReadInt32BigEndian(segmentData.Slice(5, 4));

        // Grid origin (in 1/256 pixel units, signed)
        int gridOffsetX = BinaryPrimitives.ReadInt32BigEndian(segmentData.Slice(9, 4));
        int gridOffsetY = BinaryPrimitives.ReadInt32BigEndian(segmentData.Slice(13, 4));

        // Grid step vectors (in 1/256 pixel units, unsigned 16-bit)
        int gridVectorX = BinaryPrimitives.ReadUInt16BigEndian(segmentData.Slice(17, 2));
        int gridVectorY = BinaryPrimitives.ReadUInt16BigEndian(segmentData.Slice(19, 2));

        const int headerSize = 21;

        if (gridWidth <= 0 || gridHeight <= 0)
        {
            return new Jbig2Bitmap(regionInfo.Width, regionInfo.Height);
        }

        // Sanity check to prevent OOM on corrupt data
        long gridCells = (long)gridWidth * gridHeight;
        if (gridCells > 100_000_000)
        {
            return new Jbig2Bitmap(regionInfo.Width, regionInfo.Height);
        }

        // Determine number of gray-scale bit planes
        int numberOfPatterns = patterns.Length;
        int bitsPerValue = 0;
        int temp = numberOfPatterns - 1;
        while (temp > 0)
        {
            bitsPerValue++;
            temp >>= 1;
        }

        if (bitsPerValue == 0)
        {
            bitsPerValue = 1;
        }

        // Compute HSKIP bitmap (ITU-T T.88 Section 6.6.5.1)
        // Marks grid cells whose pattern falls entirely outside the region.
        Jbig2Bitmap? skipBitmap = null;
        if (enableSkip)
        {
            skipBitmap = new Jbig2Bitmap(gridWidth, gridHeight);
            int hpw = patterns[0].Width;
            int hph = patterns[0].Height;

            for (int mg = 0; mg < gridHeight; mg++)
            {
                Span<byte> skipRow = skipBitmap.GetRow(mg);
                for (int ng = 0; ng < gridWidth; ng++)
                {
                    long x = (gridOffsetX + ((long)mg * gridVectorY) + ((long)ng * gridVectorX)) >> 8;
                    long y = (gridOffsetY + ((long)mg * gridVectorX) - ((long)ng * gridVectorY)) >> 8;

                    if (x + hpw <= 0
                        || x >= regionInfo.Width
                        || y + hph <= 0
                        || y >= regionInfo.Height)
                    {
                        skipRow[ng >> 3] |= (byte)(0x80 >> (ng & 7));
                    }
                }
            }
        }

        ReadOnlySpan<byte> codedData = segmentData.Slice(headerSize);

        // Decode gray-scale bit planes sequentially (Annex C)
        var grayScaleBitPlanes = new Jbig2Bitmap[bitsPerValue];

        if (mmr)
        {
            // MMR: planes are in one continuous stream, separated by EOFB codes
            DecodeMmrPlanes(codedData, gridWidth, gridHeight, bitsPerValue, grayScaleBitPlanes);
        }
        else
        {
            // Arithmetic: single sequential decoder for all planes
            DecodeArithmeticPlanes(codedData, gridWidth, gridHeight, bitsPerValue, templateId, skipBitmap, grayScaleBitPlanes);
        }

        // Build pattern indices using Gray-code decoding (ITU-T T.88 Section 6.6.5.2)
        int patternWidth = patterns[0].Width;
        int patternHeight = patterns[0].Height;

        Jbig2Bitmap regionBitmap = new(regionInfo.Width, regionInfo.Height, defaultPixel);

        for (int mg = 0; mg < gridHeight; mg++)
        {
            for (int ng = 0; ng < gridWidth; ng++)
            {
                // Gray-code to binary conversion (progressive XOR)
                int bit = 0;
                int patternIndex = 0;
                for (int j = bitsPerValue - 1; j >= 0; j--)
                {
                    bit ^= grayScaleBitPlanes[j].GetPixel(ng, mg);
                    patternIndex |= bit << j;
                }

                if (patternIndex < 0 || patternIndex >= numberOfPatterns)
                {
                    continue;
                }

                Jbig2Bitmap pattern = patterns[patternIndex];

                // Compute pattern position using grid vectors (1/256 pixel units)
                int x = (gridOffsetX + (mg * gridVectorY) + (ng * gridVectorX)) >> 8;
                int y = (gridOffsetY + (mg * gridVectorX) - (ng * gridVectorY)) >> 8;

                regionBitmap.Composite(pattern, x, y, combinationOp);
            }
        }

        return regionBitmap;
    }

    /// <summary>
    /// Decodes all bit planes using MMR coding from a single continuous stream.
    /// </summary>
    private static void DecodeMmrPlanes(
        in ReadOnlySpan<byte> data,
        int gridWidth,
        int gridHeight,
        int bitsPerValue,
        Jbig2Bitmap[] planes)
    {
        int offset = 0;

        for (int plane = bitsPerValue - 1; plane >= 0; plane--)
        {
            if (offset >= data.Length)
            {
                planes[plane] = new Jbig2Bitmap(gridWidth, gridHeight);
                continue;
            }

            planes[plane] = Jbig2MmrDecoder.Decode(data.Slice(offset), gridWidth, gridHeight, out int bytesConsumed);
            offset += bytesConsumed;
        }
    }

    /// <summary>
    /// Decodes all bit planes using arithmetic coding with a single sequential decoder.
    /// </summary>
    private static void DecodeArithmeticPlanes(
        in ReadOnlySpan<byte> data,
        int gridWidth,
        int gridHeight,
        int bitsPerValue,
        int templateId,
        Jbig2Bitmap? skipBitmap,
        Jbig2Bitmap[] planes)
    {
        Jbig2AtPixels atPixels = Jbig2Templates.GetDefaultAtPixels(templateId);
        Jbig2ArithmeticReader decoder = new(data);
        int contextSize = Jbig2Templates.GetContextSize(templateId);
        ReadOnlySpan<Jbig2ContextPixel> template = Jbig2Templates.ResolveTemplate(templateId, atPixels.AtX, atPixels.AtY);

        // Contexts are shared across all bit planes (the probability model accumulates).
        // The arithmetic reader is also shared (single continuous stream).
        Span<byte> contexts = new byte[contextSize];

        for (int plane = bitsPerValue - 1; plane >= 0; plane--)
        {
            Jbig2Bitmap bitmap = new(gridWidth, gridHeight);

            for (int y = 0; y < gridHeight; y++)
            {
                if (skipBitmap != null)
                {
                    DecodeRowWithSkip(ref decoder, bitmap, contexts, y, gridWidth, template, skipBitmap);
                }
                else
                {
                    Jbig2RowDecoder.DecodeRow(ref decoder, bitmap, contexts, y, gridWidth, template);
                }
            }

            planes[plane] = bitmap;
        }
    }

    /// <summary>
    /// Decodes one row of a bit plane, skipping pixels marked in the skip bitmap.
    /// Skipped pixels are left as 0 and no arithmetic decoding is performed for them.
    /// </summary>
    private static void DecodeRowWithSkip(
        ref Jbig2ArithmeticReader decoder,
        Jbig2Bitmap bitmap,
        in Span<byte> contexts,
        int y,
        int width,
        in ReadOnlySpan<Jbig2ContextPixel> templatePixels,
        Jbig2Bitmap skipBitmap)
    {
        ReadOnlySpan<byte> skipRow = skipBitmap.GetRowReadOnly(y);
        int pixelCount = templatePixels.Length;

        for (int x = 0; x < width; x++)
        {
            if ((skipRow[x >> 3] & (0x80 >> (x & 7))) != 0)
            {
                continue;
            }

            Jbig2RowDecoder.DecodeValue(ref decoder, bitmap, contexts, x, y, templatePixels);
        }
    }
}
