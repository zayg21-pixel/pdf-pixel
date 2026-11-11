using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// A decoder producing its samples row by row, decoding its tiles into the image cache.
/// </summary>
internal abstract class PdfRowImageDecoder : PdfImageDecoder
{
    private readonly PdfImageTileSelection _tiles = new();
    private PdfImageSampleInfo? _sampleInfo;
    private PdfIntegerRectangle? _regionTiles;

    protected PdfRowImageDecoder(PdfImage image)
        : base(image)
    {
    }

    /// <inheritdoc />
    protected override PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfExecutionObserver? observer)
    {
        PdfImageSampleInfo sampleInfo = SelectSamples(targetSize, observer);
        PdfIntegerRectangle regionTiles = GetRegionTileRange(sampleInfo, region);

        _tiles.SetSamples(sampleInfo, plane: 0);
        _tiles.SetCache(Cache, Image);

        if (_regionTiles != regionTiles)
        {
            _tiles.EvictOutside(regionTiles);
            _regionTiles = regionTiles;
        }

        _sampleInfo = sampleInfo;

        return new PdfImageDecodeInfo(sampleInfo, null);
    }

    /// <inheritdoc />
    /// <remarks>
    /// A tile not decoded yet is decoded in one pass with every other missing tile of the selected region.
    /// </remarks>
    public override PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer)
    {
        if (_sampleInfo == null || _regionTiles == null)
        {
            throw new InvalidOperationException("No tiles are selected.");
        }

        if (!_tiles.Contains(tileIndex))
        {
            DecodeTiles(_sampleInfo, tileIndex, _regionTiles.Value, observer);
        }

        return _tiles.GetTile(tileIndex);
    }

    /// <inheritdoc />
    protected override void DropTiles()
    {
        _tiles.Clear();
        _regionTiles = null;
    }

    /// <summary>
    /// Returns the samples the image is decoded to with at least <paramref name="targetSize"/> samples per axis.
    /// </summary>
    protected abstract PdfImageSampleInfo SelectSamples(in PdfIntegerSize targetSize, IPdfExecutionObserver? observer);

    /// <summary>
    /// Starts decoding the rows of <paramref name="samples"/>, reconstructing at least <paramref name="regions"/>, in
    /// decoded samples.
    /// </summary>
    protected abstract void BeginRows(PdfImageSampleInfo samples, IReadOnlyList<PdfIntegerRectangle> regions, IPdfExecutionObserver? observer);

    /// <summary>
    /// Reads the next row of every selected sample into <paramref name="destination"/>, the components of a
    /// sample packed together. Returns false once no further row can be produced.
    /// </summary>
    protected abstract bool TryReadRow(in Span<byte> destination, IPdfExecutionObserver? observer);

    /// <summary>
    /// Releases the state of the rows begun by <see cref="BeginRows"/>.
    /// </summary>
    protected abstract void EndRows();

    private void DecodeTiles(PdfImageSampleInfo sampleInfo, int requestedTileIndex, in PdfIntegerRectangle regionTiles, IPdfExecutionObserver? observer)
    {
        PdfTileInfo grid = sampleInfo.Grid;
        List<int> tileIndexes = [requestedTileIndex];

        for (int row = regionTiles.Top; row < regionTiles.Bottom; row++)
        {
            for (int column = regionTiles.Left; column < regionTiles.Right; column++)
            {
                int tileIndex = grid.GetTileIndex(column, row);

                if (tileIndex != requestedTileIndex && !_tiles.Contains(tileIndex))
                {
                    tileIndexes.Add(tileIndex);
                }
            }
        }

        List<PdfIntegerRectangle> regions = new(tileIndexes.Count);
        int bottom = 0;

        foreach (int tileIndex in tileIndexes)
        {
            PdfIntegerRectangle bounds = grid.GetTilePosition(tileIndex);

            regions.Add(bounds);
            bottom = Math.Max(bottom, bounds.Bottom);
            _tiles.GetRowForWriting(tileIndex, bounds.Top);
        }

        int pixelBits = sampleInfo.ComponentCount * sampleInfo.BitsPerComponent;
        var rowBuffer = new byte[PdfImageTileSelection.GetRowStride(sampleInfo, grid.ImageSize.Width)];

        try
        {
            BeginRows(sampleInfo, regions, observer);

            try
            {
                for (int row = 0; row < bottom; row++)
                {
                    if (!TryReadRow(rowBuffer, observer))
                    {
                        Logger.LogWarning("Image rows ended at row {Row} of {Height} (Reference={Reference}).", row, grid.ImageSize.Height, Image.Reference);
                        break;
                    }

                    foreach (PdfIntegerRectangle bounds in regions)
                    {
                        if (row < bounds.Top || row >= bounds.Bottom)
                        {
                            continue;
                        }

                        int tileIndex = grid.GetTileIndex(bounds.Left / grid.TileWidth, bounds.Top / grid.TileHeight);
                        Span<byte> tileRow = _tiles.GetRowForWriting(tileIndex, row);

                        rowBuffer.AsSpan((bounds.Left * pixelBits) >> 3, tileRow.Length).CopyTo(tileRow);
                    }

                    observer?.Notify();
                }
            }
            finally
            {
                EndRows();
            }
        }
        catch
        {
            foreach (int tileIndex in tileIndexes)
            {
                _tiles.Remove(tileIndex);
            }

            throw;
        }

        foreach (int tileIndex in tileIndexes)
        {
            _tiles.Complete(tileIndex);
        }
    }
}
