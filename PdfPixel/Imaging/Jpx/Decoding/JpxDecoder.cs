using PdfPixel.Imaging.Jpx.Model;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Main JPX decoder implementation that orchestrates tile parsing and decoding.
/// Converts JPX codestream into row-based output compatible with PDF image processing.
/// </summary>
internal sealed class JpxDecoder : IJpxDecoder
{
    private readonly IJpxTileDecoder _tileDecoder;
    private readonly ITileContentExtractor _tileContentExtractor;

    public JpxDecoder(IJpxTileDecoder tileDecoder)
        : this(tileDecoder, new TileContentExtractor())
    {
    }

    public JpxDecoder(IJpxTileDecoder tileDecoder, ITileContentExtractor tileContentExtractor)
    {
        _tileDecoder = tileDecoder ?? throw new ArgumentNullException(nameof(tileDecoder));
        _tileContentExtractor = tileContentExtractor ?? throw new ArgumentNullException(nameof(tileContentExtractor));
    }

    public IJpxRowProvider Decode(JpxHeader header, ReadOnlySpan<byte> codestream)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        int tilesHorizontal = (int)Math.Ceiling((double)header.Width / header.TileWidth);
        int totalTiles = tilesHorizontal * (int)Math.Ceiling((double)header.Height / header.TileHeight);

        // Phase 1: Extract concatenated tile data from codestream
        ExtractedTileContent[] extractedTiles = _tileContentExtractor.ExtractTileContents(header, codestream);

        // Phase 2: Decode each tile
        var tiles = new List<JpxTile>(totalTiles);

        for (int tileIndex = 0; tileIndex < totalTiles; tileIndex++)
        {
            var extracted = extractedTiles[tileIndex];

            if (extracted.Data == null)
            {
                tiles.Add(CreateEmptyTile(tileIndex, header, tilesHorizontal));
                continue;
            }

            try
            {
                var decodedTile = _tileDecoder.DecodeTile(extracted.TileHeader, extracted.Data);
                tiles.Add(decodedTile);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to decode tile {tileIndex}: {ex.Message}", ex);
            }
        }

        return new JpxTileToRowConverter(header, tiles);
    }

    private static JpxTile CreateEmptyTile(int tileIndex, JpxHeader header, int tilesHorizontal)
    {
        int tilesVertical = (int)Math.Ceiling((double)header.Height / header.TileHeight);

        // Create a tile header for the empty tile
        var tileHeader = new JpxTileHeader
        {
            TileIndex = (ushort)tileIndex,
            TilePartLength = 0,
            TilePartIndex = 0,
            TilePartCount = 1,
            TilesHorizontal = tilesHorizontal,
            TilesVertical = tilesVertical
        };

        // Create empty tile using the simplified constructor - it handles all initialization
        return new JpxTile(header, tileHeader);
        // Component data arrays are automatically initialized to zeros by the constructor
    }
}