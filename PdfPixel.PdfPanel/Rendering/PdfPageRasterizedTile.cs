using PdfPixel.Annotations;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Caching;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// A tile the tiler rasterized, on its way to the panel: where it lies, the settings it was rasterized with and its
/// pixels, which return to their pool when it is disposed.
/// </summary>
internal sealed class PdfPageRasterizedTile : IDisposable
{
    private readonly PdfPageTilePool _pool;

    /// <summary>
    /// Initializes the tile at <paramref name="gridPoint"/> of <paramref name="pageNumber"/>, its pixels
    /// <paramref name="samples"/> rented from <paramref name="pool"/>.
    /// </summary>
    public PdfPageRasterizedTile(
        int pageNumber,
        float scale,
        in PdfIntegerPoint gridPoint,
        bool antialias,
        PdfOptionalContentStates optionalContentStates,
        PdfAnnotationStates annotationStates,
        PdfImageSamples samples,
        PdfPageTilePool pool)
    {
        PageNumber = pageNumber;
        Scale = scale;
        GridPoint = gridPoint;
        Antialias = antialias;
        OptionalContentStates = optionalContentStates;
        AnnotationStates = annotationStates;
        Samples = samples;
        _pool = pool;
    }

    /// <summary>
    /// Gets the number of the page the tile belongs to.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the scale the tile was rasterized at.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Gets the edge length of the tile in device pixels.
    /// </summary>
    public int TileSize => _pool.TileSize;

    /// <summary>
    /// Gets the column and row of the tile on the tile grid of its scale and tile size.
    /// </summary>
    public PdfIntegerPoint GridPoint { get; }

    /// <summary>
    /// Gets whether the tile was rasterized with antialiasing.
    /// </summary>
    public bool Antialias { get; }

    /// <summary>
    /// Gets the optional content states the tile was rasterized with.
    /// </summary>
    public PdfOptionalContentStates OptionalContentStates { get; }

    /// <summary>
    /// Gets the states the annotations of the tile were rasterized in.
    /// </summary>
    public PdfAnnotationStates AnnotationStates { get; }

    /// <summary>
    /// Gets the pixels of the tile, valid until it is disposed.
    /// </summary>
    public PdfImageSamples Samples { get; }

    /// <inheritdoc />
    public void Dispose() => _pool.Return(Samples);
}
