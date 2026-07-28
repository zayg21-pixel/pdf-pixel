using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Common utilities for packet enumeration across different progression orders.
/// Provides tile dimension calculations shared by all progression order parsers.
/// </summary>
internal static class JpxPacketEnumerationHelper
{
    /// <summary>
    /// Calculates the width of a tile based on SIZ marker information.
    /// </summary>
    /// <summary>
    /// Computes the tile's bounds on the reference grid. The subband, precinct, and code-block
    /// partitions inside a tile are anchored at the reference grid's origin rather than at the
    /// tile, so consumers need the tile's position and not only its size.
    /// </summary>
    public static JpxRectangle CalculateTileBounds(JpxHeader header, JpxTileHeader tileHeader)
    {
        int tileX0 = tileHeader.TileX * (int)header.TileWidth;
        int tileY0 = tileHeader.TileY * (int)header.TileHeight;

        return new JpxRectangle(
            tileX0,
            tileY0,
            Math.Min((int)header.TileWidth, (int)header.Width - tileX0),
            Math.Min((int)header.TileHeight, (int)header.Height - tileY0));
    }

    public static int CalculateTileWidth(JpxHeader header, JpxTileHeader tileHeader)
    {
        // Use proper SIZ marker information from header
        if (header.TileWidth > 0)
        {
            // Calculate actual tile width considering tile boundaries
            uint tileIndex = tileHeader.TileIndex;
            int tilesX = tileHeader.TilesHorizontal;

            if (tilesX <= 0)
            {
                tilesX = 1;
            }

            var tileX = (int)(tileIndex % tilesX);
            uint tileStartX = header.TileOriginX + (uint)(tileX * header.TileWidth);
            uint tileEndX = Math.Min(tileStartX + header.TileWidth, header.Width);

            return (int)(tileEndX - tileStartX);
        }

        // Fallback: assume single tile spans full image width
        return (int)header.Width / Math.Max(tileHeader.TilesHorizontal, 1);
    }

    /// <summary>
    /// Calculates the height of a tile based on SIZ marker information.
    /// </summary>
    public static int CalculateTileHeight(JpxHeader header, JpxTileHeader tileHeader)
    {
        // Use proper SIZ marker information from header
        if (header.TileHeight > 0)
        {
            // Calculate actual tile height considering tile boundaries
            uint tileIndex = tileHeader.TileIndex;
            int tilesX = tileHeader.TilesHorizontal;

            if (tilesX <= 0)
            {
                tilesX = 1;
            }

            var tileY = (int)(tileIndex / tilesX);
            uint tileStartY = header.TileOriginY + (uint)(tileY * header.TileHeight);
            uint tileEndY = Math.Min(tileStartY + header.TileHeight, header.Height);

            return (int)(tileEndY - tileStartY);
        }

        // Fallback: assume single tile spans full image height
        return (int)header.Height / Math.Max(tileHeader.TilesVertical, 1);
    }
}
