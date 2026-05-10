using PdfPixel.Imaging.Jpx.Model;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Interface for decoding individual JPX tiles.
/// </summary>
internal interface IJpxTileDecoder
{
    /// <summary>
    /// Decodes a single tile from tile-part data.
    /// </summary>
    /// <param name="tileHeader">Tile-specific header information.</param>
    /// <param name="tileData">Encoded tile data.</param>
    /// <param name="decodingParameters">Parameters controlling decoding resolution.</param>
    /// <returns>Decoded tile with component data.</returns>
    JpxTile DecodeTile(JpxTileHeader tileHeader, ReadOnlySpan<byte> tileData, JpxDecodingParameters decodingParameters);
}