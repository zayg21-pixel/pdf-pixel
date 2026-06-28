using SkiaSharp;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// A single decoded region of a PDF image, positioned in original image coordinates.
/// </summary>
public sealed class PdfImageTile : IDisposable
{
    /// <summary>
    /// Creates a tile descriptor with all decoded results.
    /// </summary>
    /// <param name="tileIndex">Zero-based index of this tile in row-major order.</param>
    /// <param name="tilePosition">Position and size of this tile in original image coordinates.</param>
    /// <param name="image">Decoded image for this tile, or null when <paramref name="isSkipped"/> is true.</param>
    /// <param name="parameters">Decoding parameters used to produce this tile, or null when skipped.</param>
    /// <param name="isSkipped">True when this tile was outside the region of interest and was not decoded.</param>
    /// <param name="sourceRegion">Source region within <paramref name="image"/> when atlased, or null for standalone images.</param>
    public PdfImageTile(int tileIndex, SKRectI tilePosition, SKImage? image, PdfImageRowDecodingParameters? parameters, bool isSkipped, SKRectI? sourceRegion = null)
    {
        TileIndex = tileIndex;
        TilePosition = tilePosition;
        Image = image;
        Parameters = parameters;
        IsSkipped = isSkipped;
        SourceRegion = sourceRegion;
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
    public SKImage? Image { get; }

    /// <summary>
    /// Decoding parameters used to produce this tile, in the tile's own coordinate space.
    /// </summary>
    public PdfImageRowDecodingParameters? Parameters { get; }

    /// <summary>
    /// True when this tile was outside the requested region of interest and was not decoded.
    /// </summary>
    public bool IsSkipped { get; }

    /// <summary>
    /// Source region within <see cref="Image"/> when the image is part of an atlas.
    /// Null when the image is standalone and should be used in full.
    /// </summary>
    public SKRectI? SourceRegion { get; }

    /// <summary>
    /// Returns the source rectangle for drawing — either the atlas region or the full image bounds.
    /// </summary>
    public SKRectI GetSourceRect()
    {
        if (SourceRegion != null)
        {
            return SourceRegion.Value;
        }

        return SKRectI.Create(0, 0, Image?.Width ?? 0, Image?.Height ?? 0);
    }

    /// <inheritdoc/>
    public void Dispose() => Image?.Dispose();
}
