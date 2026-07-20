using PdfPixel.Geometry;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Precomputed tile grid metrics for a PDF image: tile dimensions clamped to the image boundary,
/// and the total tile counts in each axis.
/// </summary>
public sealed class PdfTileInfo
{
    /// <summary>
    /// Computes the tile grid for an image of <paramref name="imageSize"/> divided into tiles of
    /// at most <paramref name="tileSize"/>. Tile dimensions are clamped so they never exceed the
    /// corresponding image dimension.
    /// </summary>
    /// <param name="imageSize">Full size of the image in samples.</param>
    /// <param name="tileSize">Requested maximum tile size in samples.</param>
    public PdfTileInfo(in PdfIntegerSize imageSize, in PdfIntegerSize tileSize)
    {
        ImageSize = imageSize;
        TileWidth = Math.Min(tileSize.Width, imageSize.Width);
        TileHeight = Math.Min(tileSize.Height, imageSize.Height);
        TilesHorizontal = (imageSize.Width + TileWidth - 1) / TileWidth;
        TilesVertical = (imageSize.Height + TileHeight - 1) / TileHeight;
        TotalTiles = TilesHorizontal * TilesVertical;
    }

    /// <summary>
    /// Full size of the image this grid was computed for, in samples. Tile positions returned by
    /// <see cref="GetTilePosition"/> are expressed in this coordinate space, regardless of the
    /// resolution at which the image is actually decoded.
    /// </summary>
    public PdfIntegerSize ImageSize { get; }

    /// <summary>
    /// Width of each tile in samples, clamped to the image width.
    /// </summary>
    public int TileWidth { get; }

    /// <summary>
    /// Height of each tile in samples, clamped to the image height.
    /// </summary>
    public int TileHeight { get; }

    /// <summary>
    /// Number of tile columns across the full image width.
    /// </summary>
    public int TilesHorizontal { get; }

    /// <summary>
    /// Number of tile rows across the full image height.
    /// </summary>
    public int TilesVertical { get; }

    /// <summary>
    /// Total number of tiles in the grid (<see cref="TilesHorizontal"/> × <see cref="TilesVertical"/>).
    /// </summary>
    public int TotalTiles { get; }

    /// <summary>
    /// Returns the position and size of the tile at <paramref name="tileIndex"/> in original
    /// (un-descaled, un-downscaled) image coordinates — i.e. where the tile must be placed.
    /// </summary>
    /// <param name="tileIndex">Zero-based index of the tile in row-major order.</param>
    public PdfIntegerRectangle GetTilePosition(int tileIndex)
    {
        int column = tileIndex % TilesHorizontal;
        int row = tileIndex / TilesHorizontal;
        int x = column * TileWidth;
        int y = row * TileHeight;
        int width = Math.Min(TileWidth, ImageSize.Width - x);
        int height = Math.Min(TileHeight, ImageSize.Height - y);
        return new PdfIntegerRectangle(x, y, x + width, y + height);
    }
}
