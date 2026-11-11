using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Objects;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Base class for PDF image decoders.
/// </summary>
internal abstract class PdfImageDecoder : IPdfImageDecoder
{
    /// <summary>
    /// The key of samples decoded whole, at their stored size.
    /// </summary>
    protected static readonly PdfImageCacheKey WholeImageKey = new(plane: 0, descaleFactor: 1, part: 0);

    private IPdfImageCache? _cache;

    /// <summary>
    /// Initializes the base decoder of <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The PDF image descriptor to decode.</param>
    protected PdfImageDecoder(PdfImage image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
        LoggerFactory = image.Document.LoggerFactory;
        Logger = LoggerFactory.CreateLogger(GetType());
    }

    /// <summary>
    /// Source PDF image to decode.
    /// </summary>
    protected PdfImage Image { get; }

    /// <summary>
    /// Gets the cache the selected tiles are decoded into and kept in.
    /// </summary>
    /// <exception cref="InvalidOperationException">No tiles are selected.</exception>
    protected IPdfImageCache Cache => _cache ?? throw new InvalidOperationException("No tiles are selected.");

    /// <inheritdoc />
    public PdfImageMaskKind MaskKind => Image.MaskKind;

    /// <inheritdoc />
    public bool HasImageMask => Image.HasImageMask;

    /// <inheritdoc />
    public PdfRange[]? Decode => Image.Decode;

    /// <inheritdoc />
    public bool Interpolate => Image.Interpolate;

    /// <inheritdoc />
    public int[]? ColorKeyMask => Image.ColorKeyMask;

    /// <inheritdoc />
    public float[]? Matte => Image.SoftMask?.Matte;

    /// <summary>
    /// Logger instance for this decoder.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Logger factory instance.
    /// </summary>
    protected ILoggerFactory LoggerFactory { get; }

    /// <summary>
    /// Factory: create an appropriate image decoder for the given <see cref="PdfImage"/> based on its <see cref="PdfImage.Type"/>.
    /// Returns null for unsupported encodings.
    /// </summary>
    /// <param name="pdfImage">The image descriptor to decode.</param>
    /// <returns>A concrete <see cref="PdfImageDecoder"/> instance, or null if unsupported.</returns>
    public static PdfImageDecoder? GetDecoder(PdfImage pdfImage)
    {
        if (pdfImage == null)
        {
            throw new ArgumentNullException(nameof(pdfImage));
        }

        switch (pdfImage.Type)
        {
            case PdfImageType.Raw:
                return new RawImageDecoder(pdfImage);

            case PdfImageType.JPEG:
                return new JpegImageDecoder(pdfImage);

            case PdfImageType.JPEG2000:
                return new JpxImageDecoder(pdfImage);

            case PdfImageType.CCITT:
                return new CcittImageDecoder(pdfImage);

            case PdfImageType.JBIG2:
                return new Jbig2ImageDecoder(pdfImage);

            default:
                return null;
        }
    }

    /// <inheritdoc />
    public PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfImageCache cache, IPdfExecutionObserver? observer)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        return SelectTiles(targetSize, region, observer);
    }

    /// <inheritdoc />
    public abstract PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer);

    /// <inheritdoc />
    public virtual PdfImageTile? GetAlphaTile(int tileIndex, IPdfExecutionObserver? observer) => null;

    /// <inheritdoc />
    public void ReleaseTiles()
    {
        DropTiles();

        if (_cache != null)
        {
            _cache.Release(Image);
            _cache = null;
        }
    }

    /// <summary>
    /// Selects the tiles <see cref="GetTile"/> returns until the next selection, decoded into and kept in
    /// <see cref="Cache"/>.
    /// </summary>
    protected abstract PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfExecutionObserver? observer);

    /// <summary>
    /// Drops the decoder's references to the selected tiles, which go back to the cache.
    /// </summary>
    protected virtual void DropTiles()
    {
    }

    /// <summary>
    /// Returns the tile grid over <paramref name="sampleSize"/> decoded samples, in tiles of the cache's tile size
    /// rounded up to whole bytes of samples.
    /// </summary>
    protected PdfTileInfo CreateGrid(in PdfIntegerSize sampleSize)
    {
        int tileSize = (Math.Max(1, Cache.TileSize) + 7) & ~7;

        return new PdfTileInfo(sampleSize, new PdfIntegerSize(tileSize, tileSize));
    }

    /// <summary>
    /// Returns a view over the tile at <paramref name="tileIndex"/> of samples held whole in <paramref name="data"/>,
    /// <paramref name="rowStride"/> bytes per row.
    /// </summary>
    protected static PdfImageTile CreateBitmapTile(PdfImageSampleInfo samples, in ReadOnlyMemory<byte> data, int rowStride, int tileIndex)
    {
        PdfIntegerRectangle bounds = samples.Grid.GetTilePosition(tileIndex);

        return new PdfImageTile(data.Slice(bounds.Top * rowStride), rowStride, bounds, 0, samples.BitsPerComponent, samples.ComponentCount);
    }

    /// <summary>
    /// Returns the tile columns and rows of <paramref name="samples"/> that <paramref name="region"/> of the unit square
    /// reaches, rounded outward.
    /// </summary>
    protected static PdfIntegerRectangle GetRegionTileRange(PdfImageSampleInfo samples, PdfRectangle? region)
    {
        PdfIntegerSize size = samples.Grid.ImageSize;
        PdfIntegerRectangle allSamples = new(0, 0, size.Width, size.Height);

        if (region == null)
        {
            return samples.Grid.GetTileRange(allSamples);
        }

        PdfRectangle unitRegion = region.Value;
        PdfIntegerRectangle sampleRegion = new(
            (int)Math.Floor(Math.Max(0, Math.Min(1, unitRegion.Left)) * size.Width),
            (int)Math.Floor(Math.Max(0, Math.Min(1, unitRegion.Top)) * size.Height),
            (int)Math.Ceiling(Math.Max(0, Math.Min(1, unitRegion.Right)) * size.Width),
            (int)Math.Ceiling(Math.Max(0, Math.Min(1, unitRegion.Bottom)) * size.Height));

        return samples.Grid.GetTileRange(PdfIntegerRectangle.Intersect(sampleRegion, allSamples));
    }

    /// <summary>
    /// Maps regions of the grid samples are decoded on at <paramref name="descaleFactor"/> onto the stored sample grid,
    /// widened by one decoded sample on each side.
    /// </summary>
    /// <param name="regions">Regions in decoded samples.</param>
    /// <param name="descaleFactor">Power-of-two reduction the samples are reconstructed at.</param>
    protected static List<PdfIntegerRectangle> MapRegionsToStoredGrid(IReadOnlyList<PdfIntegerRectangle> regions, int descaleFactor)
    {
        List<PdfIntegerRectangle> mappedRegions = new(regions.Count);

        for (int index = 0; index < regions.Count; index++)
        {
            PdfIntegerRectangle region = regions[index];

            mappedRegions.Add(new PdfIntegerRectangle(
                (region.Left - 1) * descaleFactor,
                (region.Top - 1) * descaleFactor,
                (region.Right + 1) * descaleFactor,
                (region.Bottom + 1) * descaleFactor));
        }

        return mappedRegions;
    }

    /// <summary>
    /// Returns the largest power-of-two reduction, up to <paramref name="maxDescaleFactor"/>, whose reconstruction
    /// still carries at least <paramref name="targetSize"/> samples per axis.
    /// </summary>
    protected static int SelectDescaleFactor(
        in PdfIntegerSize sampleSize,
        in PdfIntegerSize targetSize,
        PdfColorSpaceConverter colorSpaceConverter,
        int maxDescaleFactor)
    {
        // Indexed samples are palette indices; never reconstruct them at a reduced size.
        if (colorSpaceConverter is PdfIndexedColorSpaceConverter)
        {
            return 1;
        }

        int descaleFactor = 1;
        for (int candidate = 2; candidate <= maxDescaleFactor; candidate *= 2)
        {
            if (Descale(sampleSize.Width, candidate) < targetSize.Width
                || Descale(sampleSize.Height, candidate) < targetSize.Height)
            {
                break;
            }

            descaleFactor = candidate;
        }

        return descaleFactor;
    }

    /// <summary>
    /// Sample count left of <paramref name="sampleCount"/> after reducing by <paramref name="descaleFactor"/>.
    /// </summary>
    /// <param name="sampleCount">Stored sample count.</param>
    /// <param name="descaleFactor">Power-of-two reduction.</param>
    protected static int Descale(int sampleCount, int descaleFactor) => Math.Max(1, (sampleCount + descaleFactor - 1) / descaleFactor);

    /// <inheritdoc />
    public override string ToString()
        => $"{Image.Width.ToString(CultureInfo.InvariantCulture)}x{Image.Height.ToString(CultureInfo.InvariantCulture)}";
}
