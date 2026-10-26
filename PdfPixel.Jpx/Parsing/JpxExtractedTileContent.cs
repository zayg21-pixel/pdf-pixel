using PdfPixel.Jpx.Decoding;
using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Represents a single tile's extracted content: its header and the concatenated
/// byte data from all tile-parts, ready to be passed directly to <see cref="JpxTileDecoder"/>.
/// </summary>
internal readonly struct JpxExtractedTileContent
{
    public JpxExtractedTileContent(JpxTileHeader tileHeader, in ReadOnlyMemory<byte> data)
    {
        TileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
        Data = data;
    }

    /// <summary>
    /// Gets the tile header parsed from the SOT marker segment.
    /// </summary>
    public JpxTileHeader TileHeader { get; }

    /// <summary>
    /// Gets the concatenated tile-part data for this tile.
    /// The first tile-part's data is kept as-is (including SOD marker).
    /// Subsequent tile-parts have their SOD markers stripped so the result
    /// is a single contiguous packet data stream after the initial SOD.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; }
}
