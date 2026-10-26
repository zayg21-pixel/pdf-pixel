using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PdfPixel.Jbig2.Decoding;

namespace PdfPixel.Jbig2.Parsing;

/// <summary>
/// Parses JBIG2 segment headers from a byte stream (ITU-T T.88 Section 7.2), and the
/// header fields at the start of segment data.
/// Handles both the file format and the embedded format.
/// </summary>
internal sealed class Jbig2SegmentParser
{
    /// <summary>
    /// JBIG2 file header signature (8 bytes): 0x97 0x4A 0x42 0x32 0x0D 0x0A 0x1A 0x0A.
    /// </summary>
    private static ReadOnlySpan<byte> FileHeaderSignature => [0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Parses all segment headers from the given data. The embedded format carries no file header.
    /// </summary>
    /// <param name="data">Full JBIG2 data (globals + page data concatenated).</param>
    /// <returns>List of parsed segment headers.</returns>
    public List<Jbig2SegmentHeader> ParseSegments(in ReadOnlySpan<byte> data)
    {
        List<Jbig2SegmentHeader> segments = [];
        int offset = 0;

        // Check for file header (not present in the embedded format)
        if (data.Length >= 8 && data.Slice(0, 8).SequenceEqual(FileHeaderSignature))
        {
            // Skip file header: 8 bytes signature + 1 byte flags + optional 4 bytes page count.
            // Flags bit 0 is the organization, bit 1 clear means the page count follows (T.88 D.4.2).
            byte flags = data[8];
            offset = 9;
            bool knownPageCount = (flags & 0x02) == 0;
            if (knownPageCount)
            {
                offset += 4;
            }

            // TODO: [MEDIUM] Support random-access organization (flags bit 0 clear), which stores
            // every segment header first and all segment data after them. The loop below assumes
            // the sequential organization, where each header is followed by its own data.
        }

        while (offset < data.Length)
        {
            Jbig2SegmentHeader? header = ParseSegmentHeader(data, ref offset);
            if (header == null)
            {
                break;
            }

            segments.Add(header);

            if (header.Type == Jbig2SegmentType.EndOfFile)
            {
                break;
            }
        }

        return segments;
    }

    private Jbig2SegmentHeader? ParseSegmentHeader(in ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 6 > data.Length)
        {
            return null;
        }

        Jbig2SegmentHeader header = new();

        // Segment number (4 bytes, big-endian)
        header.SegmentNumber = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
        offset += 4;

        // Segment header flags (1 byte)
        byte flags = data[offset];
        offset++;

        int typeValue = flags & 0x3F;
        header.Type = (Jbig2SegmentType)typeValue;
        header.PageAssociationSizeLong = (flags & 0x40) != 0;
        header.RetainFlag = (flags & 0x80) != 0;

        // Referred-to segment count
        if (offset >= data.Length)
        {
            return null;
        }

        byte referredByte = data[offset];
        offset++;

        int referredCount = (referredByte >> 5) & 0x07;
        if (referredCount == 7)
        {
            // Long form: next 4 bytes contain the count (minus the high 3 bits already read)
            if (offset + 3 > data.Length)
            {
                return null;
            }

            referredCount = ((referredByte & 0x1F) << 24)
                | (data[offset] << 16)
                | (data[offset + 1] << 8)
                | data[offset + 2];
            offset += 3;

            // Skip retain flags
            int retainBytes = (referredCount + 8) >> 3;
            offset += retainBytes;
        }
        else
        {
            // Short form retain/refer flags are packed in the same byte (bits 0-4)
            // No extra bytes needed for small counts
        }

        // Read referred-to segment numbers
        header.ReferredToSegments = new uint[referredCount];
        int segNumSize = (header.SegmentNumber <= 256) ? 1 : ((header.SegmentNumber <= 65536) ? 2 : 4);

        for (int i = 0; i < referredCount; i++)
        {
            if (offset + segNumSize > data.Length)
            {
                return null;
            }

            header.ReferredToSegments[i] = segNumSize switch
            {
                1 => data[offset],
                2 => BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2)),
                _ => BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4))
            };
            offset += segNumSize;
        }

        // Page association
        if (header.PageAssociationSizeLong)
        {
            if (offset + 4 > data.Length)
            {
                return null;
            }

            header.PageAssociation = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset, 4));
            offset += 4;
        }
        else
        {
            if (offset >= data.Length)
            {
                return null;
            }

            header.PageAssociation = data[offset];
            offset++;
        }

        // Data length (4 bytes)
        if (offset + 4 > data.Length)
        {
            return null;
        }

        uint dataLength = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
        offset += 4;

        if (dataLength == 0xFFFFFFFF)
        {
            // 7.2.7 Unknown segment data length: only ImmediateGenericRegion (type 38) supports this.
            // We scan forward for the end-of-data marker by matching a 6-byte pattern consisting of
            // the arithmetic-coding end marker (0xFF 0xAC) followed by the 4-byte big-endian region height,
            // or just the 4-byte height (bytes 2-5 of the pattern) when MMR coding is used.
            if (header.Type != Jbig2SegmentType.ImmediateGenericRegion)
            {
                return null;
            }

            // Region segment information field is 17 bytes: width(4)+height(4)+x(4)+y(4)+flags(1).
            // Height is at offset + 4; segment flags (MMR = bit 0) is at offset + 16.
            const int regionInfoFieldLength = 17;
            if (offset + regionInfoFieldLength > data.Length)
            {
                return null;
            }

            // Generic region segment flags byte immediately follows the 17-byte region info field.
            bool isMmr = (data[offset + regionInfoFieldLength] & 0x01) != 0;

            const int endMarkerPrefixLength = 2;
            const int endMarkerTotalLength = 6; // 2-byte marker + 4-byte row count
            ReadOnlySpan<byte> endMarker = isMmr ? [0x00, 0x00] : [0xFF, 0xAC];

            // The end marker can appear anywhere after the 18th byte of the segment data part.
            const int minDataBeforeMarker = 18;
            int searchStart = offset + minDataBeforeMarker;

            int matchIndex = -1;
            for (int searchPos = searchStart; searchPos <= data.Length - endMarkerTotalLength; searchPos++)
            {
                if (data.Slice(searchPos, endMarkerPrefixLength).SequenceEqual(endMarker))
                {
                    matchIndex = searchPos;
                    break;
                }
            }

            if (matchIndex < 0)
            {
                return null;
            }

            // Read the actual row count from the 4 bytes following the 2-byte end marker.
            if (matchIndex + endMarkerPrefixLength + 4 > data.Length)
            {
                return null;
            }

            header.ActualRowCount = BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(matchIndex + endMarkerPrefixLength, 4));
            header.DataLength = (matchIndex + endMarkerTotalLength) - offset;
        }
        else
        {
            header.DataLength = dataLength;
        }

        header.DataOffset = offset;

        // Advance past the data
        offset += (int)header.DataLength;

        return header;
    }

    /// <summary>
    /// Parses the 17-byte region segment information field from the start of <paramref name="data"/>.
    /// </summary>
    /// <param name="segment">Segment header; supplies <see cref="Jbig2SegmentHeader.ActualRowCount"/> override for height.</param>
    /// <param name="data">Segment data starting at the region information field.</param>
    /// <returns>Parsed region header.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="data"/> is shorter than <see cref="Jbig2RegionHeader.Jbig2RegionHeaderLength"/> bytes.
    /// </exception>
    public static Jbig2RegionHeader ParseRegionHeader(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> data)
    {
        if (data.Length < Jbig2RegionHeader.Jbig2RegionHeaderLength)
        {
            throw new InvalidOperationException(
                $"JBIG2 region segment data too short: expected at least {Jbig2RegionHeader.Jbig2RegionHeaderLength} bytes, got {data.Length}.");
        }

        return new Jbig2RegionHeader
        {
            Width = BinaryPrimitives.ReadInt32BigEndian(data.Slice(0, 4)),
            Height = (segment.ActualRowCount.HasValue)
                ? (int)segment.ActualRowCount.Value
                : BinaryPrimitives.ReadInt32BigEndian(data.Slice(4, 4)),
            X = BinaryPrimitives.ReadInt32BigEndian(data.Slice(8, 4)),
            Y = BinaryPrimitives.ReadInt32BigEndian(data.Slice(12, 4)),
            CombinationOperator = (Jbig2CombinationOperator)(data[16] & 0x07)
        };
    }

    /// <summary>
    /// Parses the page information segment, or returns null when its data is too short.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Jbig2PageInfo? ParsePageInformation(Jbig2SegmentHeader segment, in ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<byte> segData = data.Slice(segment.DataOffset, (int)segment.DataLength);

        if (segData.Length < 19)
        {
            return null;
        }


        Jbig2PageInfo info = new()
        {
            Width = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(0, 4)),
            Height = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(4, 4)),
            XResolution = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(8, 4)),
            YResolution = BinaryPrimitives.ReadInt32BigEndian(segData.Slice(12, 4))
        };

        byte flags = segData[16];
        info.DefaultPixelValue = (byte)((flags >> 2) & 1);
        info.CombinationOperator = (Jbig2CombinationOperator)((flags >> 3) & 0x03);
        info.RequiresBuffer = (flags & 0x20) == 0;

        return info;
    }

    /// <summary>
    /// Parses the segment header from the supplied data span and returns a populated
    /// <see cref="Jbig2SymbolDictionarySegmentInfo"/>.
    /// </summary>
    /// <param name="data">Segment data beginning at the first byte of the 2-byte flags word.</param>
    /// <returns>Parsed segment info.</returns>
    public static Jbig2SymbolDictionarySegmentInfo ParseSymbolDictionaryInfo(in ReadOnlySpan<byte> data)
    {
        var flagsWord = (ushort)((data[0] << 8) | data[1]);
        Jbig2SymbolDictionaryFlags flags = new(flagsWord);

        // Layout after the 2-byte flags word:
        //   [AT pixel pairs  (AtPixelCount(Template) * 2 bytes, only when !UseHuffman)]
        //   [refinement AT   (4 bytes, only when UseRefinementAggregation && RefinementTemplate == 0)]
        //   exported symbol count (4 bytes)
        //   new symbol count      (4 bytes)
        int offset = 2;
        int atCount = (flags.UseHuffman) ? 0 : Jbig2Templates.AtPixelCount(flags.Template);
        Jbig2AtPixels? atPixels = (atCount > 0)
            ? ReadAtPixelPairs(data.Slice(offset), atCount)
            : null;
        offset += atCount * 2;

        bool hasRefinAt = flags.UseRefinementAggregation && flags.RefinementTemplate == 0;
        Jbig2AtPixels? refinementAtPixels = hasRefinAt
            ? ReadAtPixelPairs(data.Slice(offset), 2)
            : null;
        offset += hasRefinAt ? 4 : 0;

        int exportedSymbolCount = 0;
        int newSymbolCount = 0;
        if (offset + 7 < data.Length)
        {
            exportedSymbolCount = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset));
            offset += 4;
            newSymbolCount = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset));
            offset += 4;
        }

        return new Jbig2SymbolDictionarySegmentInfo(
            flags,
            atPixels,
            refinementAtPixels,
            exportedSymbolCount,
            newSymbolCount,
            offset);
    }

    /// <summary>
    /// Parses the segment header from the supplied data span and returns a populated
    /// <see cref="Jbig2TextRegionSegmentInfo"/>.
    /// </summary>
    /// <param name="data">Segment data beginning at the first byte of the 2-byte flags word.</param>
    /// <param name="totalSymbolCount">Total number of symbols available for placement.</param>
    /// <returns>Parsed segment info.</returns>
    public static Jbig2TextRegionSegmentInfo ParseTextRegionInfo(in ReadOnlySpan<byte> data, int totalSymbolCount)
    {
        var flagsWord = (ushort)((data[0] << 8) | data[1]);
        Jbig2TextRegionFlags flags = new(flagsWord);

        // Layout after the 2-byte flags word:
        //   [Huffman table flags (2 bytes, only when UseHuffman)]
        //   [refinement AT pixels (4 bytes, only when UseRefinement && RefinementTemplate == 0)]
        //   number of symbol instances (4 bytes)
        int offset = 2;
        if (flags.UseHuffman)
        {
            offset += 2;
        }

        bool hasRefinAt = flags.UseRefinement && flags.RefinementTemplate == 0;
        Jbig2AtPixels? refinementAtPixels = hasRefinAt
            ? ReadAtPixelPairs(data.Slice(offset), 2)
            : null;
        offset += hasRefinAt ? 4 : 0;

        int numberOfSymbolInstances = 0;
        if (data.Length >= offset + 4)
        {
            numberOfSymbolInstances = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        }

        return new Jbig2TextRegionSegmentInfo(
            flags,
            refinementAtPixels,
            numberOfSymbolInstances,
            Jbig2SymbolCodeLength.Compute(totalSymbolCount));
    }

    /// <summary>
    /// Reads <paramref name="count"/> (X, Y) adaptive template pixel pairs from the front of
    /// <paramref name="data"/>. Pairs that fall outside the available data are left as zero.
    /// </summary>
    public static Jbig2AtPixels ReadAtPixelPairs(in ReadOnlySpan<byte> data, int count)
    {
        var atX = new sbyte[count];
        var atY = new sbyte[count];

        for (int i = 0; i < count; i++)
        {
            int byteIndex = i * 2;
            if (byteIndex + 1 < data.Length)
            {
                atX[i] = (sbyte)data[byteIndex];
                atY[i] = (sbyte)data[byteIndex + 1];
            }
        }

        return new Jbig2AtPixels(atX, atY);
    }
}
