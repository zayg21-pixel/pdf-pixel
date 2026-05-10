using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Default implementation that scans SOT markers, collects tile-part segments,
/// and concatenates their data into a single byte array per tile.
/// </summary>
internal sealed class TileContentExtractor : ITileContentExtractor
{
    /// <summary>
    /// Lightweight index entry pointing to a tile-part slice within the codestream.
    /// </summary>
    private readonly struct TilePartSlice
    {
        public readonly int Offset;
        public readonly int Length;

        public TilePartSlice(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }
    }

    /// <inheritdoc />
    public ExtractedTileContent[] ExtractTileContents(JpxHeader header, ReadOnlySpan<byte> codestream)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        int tilesHorizontal = (int)Math.Ceiling((double)header.Width / header.TileWidth);
        int tilesVertical = (int)Math.Ceiling((double)header.Height / header.TileHeight);
        int totalTiles = tilesHorizontal * tilesVertical;

        // Store lightweight slice indices instead of copying byte arrays during scan.
        // For single-part tiles (common case), firstSlice alone is sufficient.
        var firstSlices = new TilePartSlice[totalTiles];
        var hasData = new bool[totalTiles];
        var additionalSlices = new List<TilePartSlice>[totalTiles];
        var tileHeaders = new JpxTileHeader[totalTiles];
        var reader = new JpxSpanReader(codestream);

        while (!reader.EndOfSpan && reader.Remaining >= 2)
        {
            ushort marker = reader.PeekUInt16BE();

            if (marker == JpxMarkers.EOC)
            {
                break;
            }

            if (marker != JpxMarkers.SOT)
            {
                // Skip unexpected bytes
                reader.Skip(1);
                continue;
            }

            var tileHeader = ParseTileHeader(ref reader, tilesHorizontal, tilesVertical);
            int tileDataOffset = reader.Position;
            int tileDataLength = (int)tileHeader.TilePartLength - 12;

            if (tileDataLength <= 0 || reader.Remaining < tileDataLength)
            {
                throw new InvalidDataException(
                    $"Invalid tile data length: {tileDataLength}, remaining: {reader.Remaining}.");
            }

            reader.Skip(tileDataLength);

            int tileIndex = tileHeader.TileIndex;
            var slice = new TilePartSlice(tileDataOffset, tileDataLength);

            if (!hasData[tileIndex])
            {
                firstSlices[tileIndex] = slice;
                hasData[tileIndex] = true;
                tileHeaders[tileIndex] = tileHeader;
            }
            else
            {
                if (additionalSlices[tileIndex] == null)
                {
                    additionalSlices[tileIndex] = new List<TilePartSlice>();
                }

                additionalSlices[tileIndex].Add(slice);
            }
        }

        // Build result array — extract bytes from codestream using stored slices
        var results = new ExtractedTileContent[totalTiles];

        for (int tileIndex = 0; tileIndex < totalTiles; tileIndex++)
        {
            if (!hasData[tileIndex])
            {
                // Leave as default (Data will be null)
                continue;
            }

            TilePartSlice first = firstSlices[tileIndex];
            List<TilePartSlice> extra = additionalSlices[tileIndex];

            if (extra == null)
            {
                // Single tile-part: slice directly from codestream, single allocation
                byte[] data = codestream.Slice(first.Offset, first.Length).ToArray();
                results[tileIndex] = new ExtractedTileContent(tileHeaders[tileIndex], data);
            }
            else
            {
                byte[] concatenatedData = ConcatenateSlices(codestream, first, extra);
                results[tileIndex] = new ExtractedTileContent(tileHeaders[tileIndex], concatenatedData);
            }
        }

        return results;
    }

    /// <summary>
    /// Concatenates tile-part slices from the codestream into a single byte array.
    /// The first part is kept as-is (including its SOD marker for the tile decoder).
    /// Subsequent parts have their SOD marker (0xFF93) stripped since the tile decoder
    /// expects a single contiguous packet data stream after the initial SOD.
    /// </summary>
    private static byte[] ConcatenateSlices(
        ReadOnlySpan<byte> codestream,
        TilePartSlice first,
        List<TilePartSlice> additionalParts)
    {
        int totalLength = first.Length;

        for (int i = 0; i < additionalParts.Count; i++)
        {
            TilePartSlice slice = additionalParts[i];
            int partLength = slice.Length;

            // Strip SOD marker (2 bytes) from subsequent parts if present
            if (partLength >= 2 &&
                codestream[slice.Offset] == 0xFF &&
                codestream[slice.Offset + 1] == 0x93)
            {
                partLength -= 2;
            }

            totalLength += partLength;
        }

        byte[] result = new byte[totalLength];
        int offset = 0;

        // Copy first part as-is (includes SOD)
        codestream.Slice(first.Offset, first.Length).CopyTo(result.AsSpan(offset));
        offset += first.Length;

        // Copy subsequent parts, stripping SOD markers
        for (int i = 0; i < additionalParts.Count; i++)
        {
            TilePartSlice slice = additionalParts[i];
            int sourceOffset = slice.Offset;
            int copyLength = slice.Length;

            if (copyLength >= 2 &&
                codestream[sourceOffset] == 0xFF &&
                codestream[sourceOffset + 1] == 0x93)
            {
                sourceOffset += 2;
                copyLength -= 2;
            }

            if (copyLength > 0)
            {
                codestream.Slice(sourceOffset, copyLength).CopyTo(result.AsSpan(offset));
                offset += copyLength;
            }
        }

        return result;
    }

    private static JpxTileHeader ParseTileHeader(ref JpxSpanReader reader, int tilesHorizontal, int tilesVertical)
    {
        // Expect SOT marker
        ushort sotMarker = reader.ReadUInt16BE();
        if (sotMarker != JpxMarkers.SOT)
        {
            throw new InvalidDataException($"Expected SOT marker (0x{JpxMarkers.SOT:X4}), found 0x{sotMarker:X4}.");
        }

        // Parse SOT segment
        ushort segmentLength = reader.ReadUInt16BE();
        if (segmentLength != 10)
        {
            throw new InvalidDataException($"SOT segment must be 10 bytes, found {segmentLength}.");
        }

        var tileHeader = new JpxTileHeader
        {
            TileIndex = reader.ReadUInt16BE(),
            TilePartLength = reader.ReadUInt32BE(),
            TilePartIndex = reader.ReadByte(),
            TilePartCount = reader.ReadByte(),
            TilesHorizontal = tilesHorizontal,
            TilesVertical = tilesVertical
        };

        return tileHeader;
    }
}
