using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Parsing;

/// <summary>
/// Parses JBIG2 segment headers from a byte stream (ITU-T T.88 Section 7.2).
/// Handles both file-header and embedded (PDF) format.
/// </summary>
internal sealed class Jbig2SegmentParser
{
    /// <summary>
    /// JBIG2 file header signature (8 bytes): 0x97 0x4A 0x42 0x32 0x0D 0x0A 0x1A 0x0A.
    /// </summary>
    private static ReadOnlySpan<byte> FileHeaderSignature => [0x97, 0x4A, 0x42, 0x32, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Parses all segment headers from the given data. In PDF context, there is no file header.
    /// </summary>
    /// <param name="data">Full JBIG2 data (globals + page data concatenated).</param>
    /// <returns>List of parsed segment headers.</returns>
    public List<Jbig2SegmentHeader> ParseSegments(ReadOnlySpan<byte> data)
    {
        var segments = new List<Jbig2SegmentHeader>();
        int offset = 0;

        // Check for file header (not present in PDF embedded JBIG2)
        if (data.Length >= 8 && data.Slice(0, 8).SequenceEqual(FileHeaderSignature))
        {
            // Skip file header: 8 bytes signature + 1 byte flags + optional 4 bytes page count
            byte flags = data[8];
            offset = 9;
            bool knownPageCount = (flags & 0x01) == 0;
            if (knownPageCount)
            {
                offset += 4;
            }
        }

        while (offset < data.Length)
        {
            var header = ParseSegmentHeader(data, ref offset);
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

    private Jbig2SegmentHeader ParseSegmentHeader(ReadOnlySpan<byte> data, ref int offset)
    {
        if (offset + 6 > data.Length)
        {
            return null;
        }

        var header = new Jbig2SegmentHeader();

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
        int segNumSize = header.SegmentNumber <= 256 ? 1 : (header.SegmentNumber <= 65536 ? 2 : 4);

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
            const int RegionInfoFieldLength = 17;
            if (offset + RegionInfoFieldLength > data.Length)
            {
                return null;
            }

            // Generic region segment flags byte immediately follows the 17-byte region info field.
            bool isMmr = (data[offset + RegionInfoFieldLength] & 0x01) != 0; // TODO: move to constants

            const int EndMarkerPrefixLength = 2;
            const int EndMarkerTotalLength = 6; // 2-byte marker + 4-byte row count
            ReadOnlySpan<byte> endMarker = isMmr ? [0x00, 0x00] : [0xFF, 0xAC];

            // The end marker can appear anywhere after the 18th byte of the segment data part.
            const int MinDataBeforeMarker = 18;
            int searchStart = offset + MinDataBeforeMarker;

            int matchIndex = -1;
            for (int searchPos = searchStart; searchPos <= data.Length - EndMarkerTotalLength; searchPos++)
            {
                if (data.Slice(searchPos, EndMarkerPrefixLength).SequenceEqual(endMarker))
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
            if (matchIndex + EndMarkerPrefixLength + 4 > data.Length)
            {
                return null;
            }

            header.ActualRowCount = BinaryPrimitives.ReadUInt32BigEndian(
                data.Slice(matchIndex + EndMarkerPrefixLength, 4));
            header.DataLength = (matchIndex + EndMarkerTotalLength) - offset;
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
}
