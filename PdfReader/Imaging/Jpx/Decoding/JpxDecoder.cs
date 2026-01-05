using PdfReader.Imaging.Jpx.Model;
using PdfReader.Imaging.Jpx.Parsing;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfReader.Imaging.Jpx.Decoding;

/// <summary>
/// Main JPX decoder implementation that orchestrates tile parsing and decoding.
/// Converts JPX codestream into row-based output compatible with PDF image processing.
/// </summary>
internal sealed class JpxDecoder : IJpxDecoder
{
    private readonly IJpxTileDecoder _tileDecoder;

    public JpxDecoder(IJpxTileDecoder tileDecoder)
    {
        _tileDecoder = tileDecoder ?? throw new ArgumentNullException(nameof(tileDecoder));
    }

    public IJpxRowProvider Decode(JpxHeader header, ReadOnlySpan<byte> codestream)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        // Calculate tile grid dimensions
        int tilesHorizontal = (int)Math.Ceiling((double)header.Width / header.TileWidth);
        int tilesVertical = (int)Math.Ceiling((double)header.Height / header.TileHeight);
        int totalTiles = tilesHorizontal * tilesVertical;

        var tiles = new List<JpxTile>(totalTiles);

        // Parse codestream for tiles
        var reader = new JpxSpanReader(codestream);

        // Process each tile
        for (int expectedTileIndex = 0; expectedTileIndex < totalTiles; expectedTileIndex++)
        {
            if (reader.EndOfSpan)
            {
                // Create empty tile if no more data
                tiles.Add(CreateEmptyTile(expectedTileIndex, header, tilesHorizontal));
                continue;
            }

            try
            {
                var tileHeader = ParseTileHeader(ref reader, tilesHorizontal, tilesVertical);
                var tileData = ExtractTileData(ref reader, tileHeader);
                
                var decodedTile = _tileDecoder.DecodeTile(tileHeader, tileData);
                
                // Ensure tile is at correct index (handle out-of-order tiles)
                while (tiles.Count <= tileHeader.TileIndex)
                {
                    if (tiles.Count == tileHeader.TileIndex)
                    {
                        tiles.Add(decodedTile);
                    }
                    else
                    {
                        tiles.Add(CreateEmptyTile(tiles.Count, header, tilesHorizontal));
                    }
                }
            }
            catch (Exception ex)
            {
                // Handle tile decoding errors gracefully
                throw new InvalidDataException($"Failed to decode tile {expectedTileIndex}: {ex.Message}", ex);
            }
        }

        // Fill any missing tiles with empty ones
        while (tiles.Count < totalTiles)
        {
            tiles.Add(CreateEmptyTile(tiles.Count, header, tilesHorizontal));
        }

        return new JpxTileToRowConverter(header, tiles);
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

    private static ReadOnlySpan<byte> ExtractTileData(ref JpxSpanReader reader, JpxTileHeader tileHeader)
    {
        // Calculate tile data length (excluding SOT segment which is 12 bytes total)
        int tileDataLength = (int)tileHeader.TilePartLength - 12;
        
        if (tileDataLength <= 0 || reader.Remaining < tileDataLength)
        {
            throw new InvalidDataException($"Invalid tile data length: {tileDataLength}, remaining: {reader.Remaining}.");
        }

        return reader.ReadBytes(tileDataLength);
    }

    private static JpxTile CreateEmptyTile(int tileIndex, JpxHeader header, int tilesHorizontal)
    {
        int tileX = tileIndex % tilesHorizontal;
        int tileY = tileIndex / tilesHorizontal;
        
        // Create a tile header for the empty tile
        var tileHeader = new JpxTileHeader
        {
            TileIndex = (ushort)tileIndex,
            TilePartLength = 0,
            TilePartIndex = 0,
            TilePartCount = 1,
            TilesHorizontal = tilesHorizontal,
            TilesVertical = (int)Math.Ceiling((double)header.Height / header.TileHeight)
        };

        // Create empty tile using the simplified constructor - it handles all initialization
        return new JpxTile(header, tileHeader);
        // Component data arrays are automatically initialized to zeros by the constructor
    }
}