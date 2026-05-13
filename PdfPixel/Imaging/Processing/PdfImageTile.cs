using SkiaSharp;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// A single decoded region of a PDF image, positioned in original image coordinates.
/// </summary>
public sealed class PdfImageTile : IDisposable
{
    public PdfImageTile(int tileIndex, SKRectI tilePosition, SKImage image, bool isSkipped)
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
    public SKRectI TilePosition { get; }

    /// <summary>
    /// Decoded image for this tile, or null when <see cref="IsSkipped"/> is true.
    /// </summary>
    public SKImage Image { get; }

    /// <summary>
    /// True when this tile was outside the requested region of interest and was not decoded.
    /// </summary>
    public bool IsSkipped { get; }

    public void Dispose() => Image?.Dispose();
}
