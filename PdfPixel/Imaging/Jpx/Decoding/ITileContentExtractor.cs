using PdfPixel.Imaging.Jpx.Model;
using System;

namespace PdfPixel.Imaging.Jpx.Decoding;

/// <summary>
/// Represents a single tile's extracted content: its header and the concatenated
/// byte data from all tile-parts, ready to be passed directly to <see cref="IJpxTileDecoder"/>.
/// </summary>
internal readonly struct ExtractedTileContent
{
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
    public byte[] Data { get; }

    public ExtractedTileContent(JpxTileHeader tileHeader, byte[] data)
    {
        TileHeader = tileHeader ?? throw new ArgumentNullException(nameof(tileHeader));
        Data = data ?? throw new ArgumentNullException(nameof(data));
    }
}

/// <summary>
/// Extracts and concatenates tile-part data from a JPEG 2000 codestream.
/// Scans SOT markers, collects all tile-part segments per tile, concatenates
/// their data, and returns one <see cref="ExtractedTileContent"/> per tile found.
/// </summary>
internal interface ITileContentExtractor
{
    /// <summary>
    /// Extracts tile contents from a codestream, returning one entry per tile
    /// in tile-index order. The returned array length equals the total number of tiles
    /// in the image grid. Tiles not present in the codestream will have a null <see cref="ExtractedTileContent.Data"/>.
    /// </summary>
    /// <param name="header">Parsed JPX header containing tile grid information.</param>
    /// <param name="codestream">Codestream data starting at the first SOT marker.</param>
    /// <returns>Array of extracted tile contents indexed by tile index.</returns>
    ExtractedTileContent[] ExtractTileContents(JpxHeader header, ReadOnlySpan<byte> codestream);
}
