namespace PdfReader.Imaging.Jpx.Model;

/// <summary>
/// Represents tile-specific header information parsed from SOT marker segment.
/// </summary>
internal sealed class JpxTileHeader
{
    /// <summary>
    /// Gets or sets the tile index (Isot parameter).
    /// </summary>
    public ushort TileIndex { get; set; }

    /// <summary>
    /// Gets or sets the length of the tile-part (Psot parameter).
    /// </summary>
    public uint TilePartLength { get; set; }

    /// <summary>
    /// Gets or sets the tile-part index for this tile (TPsot parameter).
    /// </summary>
    public byte TilePartIndex { get; set; }

    /// <summary>
    /// Gets or sets the number of tile-parts for this tile (TNsot parameter).
    /// 0 means not specified in this tile-part header.
    /// </summary>
    public byte TilePartCount { get; set; }

    /// <summary>
    /// Gets the X coordinate of this tile in the tile grid.
    /// </summary>
    public int TileX => TileIndex % TilesHorizontal;

    /// <summary>
    /// Gets the Y coordinate of this tile in the tile grid.
    /// </summary>
    public int TileY => TileIndex / TilesHorizontal;

    /// <summary>
    /// Gets or sets the number of horizontal tiles (calculated from main header).
    /// </summary>
    public int TilesHorizontal { get; set; }

    /// <summary>
    /// Gets or sets the number of vertical tiles (calculated from main header).
    /// </summary>
    public int TilesVertical { get; set; }
}