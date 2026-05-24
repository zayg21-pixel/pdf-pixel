using System;
using System.Buffers.Binary;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Decodes JBIG2 pattern dictionary segments (ITU-T T.88 Section 6.7).
/// A pattern dictionary stores a grid of equal-sized patterns (used by halftone regions).
/// </summary>
internal static class Jbig2PatternDictionaryDecoder
{
    /// <summary>
    /// Decodes a pattern dictionary from the segment data.
    /// </summary>
    /// <param name="segmentData">Encoded pattern dictionary data.</param>
    /// <returns>Array of decoded pattern bitmaps, or empty array on failure.</returns>
    public static Jbig2Bitmap[] Decode(ReadOnlySpan<byte> segmentData)
    {
        if (segmentData.Length < 7)
        {
            return Array.Empty<Jbig2Bitmap>();
        }

        // Pattern dictionary flags (1 byte)
        var genericFlags = new Jbig2GenericRegionFlags(segmentData[0]);

        // Pattern width and height (1 byte each)
        int patternWidth = segmentData[1];
        int patternHeight = segmentData[2];

        // Largest gray-scale value (4 bytes big-endian at offset 3)
        uint grayMax = BinaryPrimitives.ReadUInt32BigEndian(segmentData.Slice(3, 4));
        int patternCount = (int)(grayMax + 1);

        if (patternWidth <= 0 || patternHeight <= 0 || patternCount <= 0)
        {
            return Array.Empty<Jbig2Bitmap>();
        }

        const int headerSize = 7;

        if (headerSize >= segmentData.Length)
        {
            return Array.Empty<Jbig2Bitmap>();
        }

        ReadOnlySpan<byte> codedData = segmentData.Slice(headerSize);

        // Decode as one large bitmap and split into individual patterns
        int collectiveWidth = patternWidth * patternCount;
        Jbig2Bitmap collectiveBitmap;

        if (genericFlags.UseMmr)
        {
            collectiveBitmap = Jbig2MmrDecoder.Decode(codedData, collectiveWidth, patternHeight);
        }
        else
        {
            int templateId = genericFlags.TemplateId;
            collectiveBitmap = Jbig2GenericRegionDecoder.DecodeWithAt(
                genericFlags, codedData, collectiveWidth, patternHeight,
                Jbig2Templates.GetPatternDictionaryAtPixels(templateId, patternWidth));
        }

        // Split collective bitmap into individual patterns
        var patterns = new Jbig2Bitmap[patternCount];
        for (int i = 0; i < patternCount; i++)
        {
            var pattern = new Jbig2Bitmap(patternWidth, patternHeight);
            int offsetX = i * patternWidth;

            for (int y = 0; y < patternHeight; y++)
            {
                for (int x = 0; x < patternWidth; x++)
                {
                    pattern.SetPixel(x, y, collectiveBitmap.GetPixel(offsetX + x, y));
                }
            }

            patterns[i] = pattern;
        }

        return patterns;
    }
}
