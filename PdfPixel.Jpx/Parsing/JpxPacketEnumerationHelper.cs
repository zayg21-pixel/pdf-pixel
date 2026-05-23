using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Represents coordinates for a single packet in the progression order.
/// </summary>
internal readonly struct PacketCoordinate
{
    public readonly int Layer;
    public readonly int Resolution;
    public readonly int Component;
    public readonly int PrecinctX;
    public readonly int PrecinctY;

    public PacketCoordinate(int layer, int resolution, int component, int precinctX, int precinctY)
    {
        Layer = layer;
        Resolution = resolution;
        Component = component;
        PrecinctX = precinctX;
        PrecinctY = precinctY;
    }
}

/// <summary>
/// Common utilities for packet enumeration across different progression orders.
/// Provides tile dimension calculations shared by all progression order parsers.
/// </summary>
internal static class JpxPacketEnumerationHelper
{
    /// <summary>
    /// Calculates the width of a tile based on SIZ marker information.
    /// </summary>
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

            int tileX = (int)(tileIndex % tilesX);
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
            int tilesY = tileHeader.TilesVertical;

            if (tilesX <= 0)
            {
                tilesX = 1;
            }

            if (tilesY <= 0)
            {
                tilesY = 1;
            }

            int tileY = (int)(tileIndex / tilesX);
            uint tileStartY = header.TileOriginY + (uint)(tileY * header.TileHeight);
            uint tileEndY = Math.Min(tileStartY + header.TileHeight, header.Height);

            return (int)(tileEndY - tileStartY);
        }

        // Fallback: assume single tile spans full image height
        return (int)header.Height / Math.Max(tileHeader.TilesVertical, 1);
    }
}
