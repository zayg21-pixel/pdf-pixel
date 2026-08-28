using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// A single decoded region of a PDF image, positioned in original image coordinates.
/// </summary>
public sealed class PdfImageTile
{
    /// <summary>
    /// Creates a tile descriptor with all decoded results.
    /// </summary>
    /// <param name="tileIndex">Zero-based index of this tile in row-major order.</param>
    /// <param name="tilePosition">Position and size of this tile in original image coordinates.</param>
    /// <param name="image">Decoded image for this tile.</param>
    public PdfImageTile(int tileIndex, in PdfIntegerRectangle tilePosition, PdfDecodedImage image)
        : this(tileIndex, tilePosition, image, isSkipped: false)
    {
    }

    /// <summary>
    /// Creates a tile descriptor, optionally as a skipped placeholder with no decoded results.
    /// </summary>
    /// <param name="tileIndex">Zero-based index of this tile in row-major order.</param>
    /// <param name="tilePosition">Position and size of this tile in original image coordinates.</param>
    /// <param name="image">Decoded image for this tile, or null when <paramref name="isSkipped"/> is true.</param>
    /// <param name="isSkipped">True when this tile was outside the region of interest and was not decoded.</param>
    private PdfImageTile(int tileIndex, in PdfIntegerRectangle tilePosition, PdfDecodedImage? image, bool isSkipped)
    {
        TileIndex = tileIndex;
        TilePosition = tilePosition;
        Image = image;
        IsSkipped = isSkipped;
    }

    /// <summary>
    /// Zero-based index of this tile within the full tile grid, in row-major order.
    /// </summary>
    public int TileIndex { get; }

    /// <summary>
    /// Position and size of this tile in original (unscaled) image coordinates.
    /// </summary>
    public PdfIntegerRectangle TilePosition { get; }

    /// <summary>
    /// Decoded image for this tile, or null when <see cref="IsSkipped"/> is true.
    /// </summary>
    public PdfDecodedImage? Image { get; }

    /// <summary>
    /// True when this tile was outside the requested region of interest and was not decoded.
    /// </summary>
    public bool IsSkipped { get; }

    /// <summary>
    /// Creates an empty, skipped tile placeholder for the given index, with no position or image.
    /// </summary>
    /// <param name="tileIndex">Zero-based index of this tile in row-major order.</param>
    /// <returns>An empty <see cref="PdfImageTile"/>.</returns>
    public static PdfImageTile CreateEmpty(int tileIndex) => new(tileIndex, default, null, isSkipped: true);
}
