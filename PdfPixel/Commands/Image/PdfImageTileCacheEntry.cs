using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Caches decoded tiles for one PDF image across repeated renders (e.g. scrolling).
/// </summary>
internal sealed class PdfImageTileCacheEntry
{
    private readonly CachedTile[] _tiles;
    private readonly PdfImageTileDecodingPass _pass;
    private readonly MaskAlphaRowSource? _alphaRowSource;
    private PdfMatrix? _totalMatrix;
    private PdfRectangle? _pageRegionOfInterest;
    private PdfIntegerSize? _outputSize;
    private PdfIntegerRectangle _regionTiles;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageTileCacheEntry"/> class.
    /// </summary>
    public PdfImageTileCacheEntry(PdfImageDecoder decoder, PdfTileInfo tileInfo, ILoggerFactory loggerFactory, MaskAlphaRowSource? alphaRowSource)
    {
        TileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));
        _pass = new PdfImageTileDecodingPass(decoder, tileInfo, loggerFactory);
        _alphaRowSource = alphaRowSource;

        _tiles = new CachedTile[tileInfo.TotalTiles];
        for (int tileIndex = 0; tileIndex < _tiles.Length; tileIndex++)
        {
            _tiles[tileIndex] = new CachedTile(tileIndex);
        }
    }

    /// <summary>
    /// Decoder used to produce tiles for this cache entry.
    /// </summary>
    public PdfImageDecoder Decoder => _pass.Decoder;

    /// <summary>
    /// Gets the tiling layout shared by every tile in this cache.
    /// </summary>
    public PdfTileInfo TileInfo { get; }

    /// <summary>
    /// Returns the tile at <paramref name="tileIndex"/> as drawn under the transformation and region of interest of
    /// <paramref name="executionContext"/>, decoding it first when it is inside that region and not decoded yet.
    /// </summary>
    public PdfImageTile GetTile(PdfCommandExecutionContext executionContext, int tileIndex)
    {
        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        if (tileIndex < 0 || tileIndex >= TileInfo.TotalTiles)
        {
            throw new ArgumentOutOfRangeException(nameof(tileIndex), $"Tile index {tileIndex} is out of range (TotalTiles={TileInfo.TotalTiles}).");
        }

        if (_totalMatrix != executionContext.Frames.TotalMatrix
            || _pageRegionOfInterest != executionContext.PageRegionOfInterest)
        {
            UpdateRegion(executionContext);
        }

        CachedTile cachedTile = _tiles[tileIndex];

        if (!cachedTile.HasImage && TileInfo.IsTileInRange(tileIndex, _regionTiles))
        {
            if (!_pass.IsActive || !cachedTile.IsPendingUpdate)
            {
                BeginDecoding(executionContext);
            }

            DecodeUntilProduced(tileIndex, executionContext.ExecutionObserver);
        }

        PdfImageTile tile = cachedTile.GetTile();

        if (!Decoder.Context.CacheDecodedTiles)
        {
            cachedTile.Clear();
        }

        return tile;
    }

    /// <summary>
    /// The grid the tiles are produced on: the samples the image and its mask carry between them,
    /// reduced to the extent the image is placed at when the device shows fewer than they hold.
    /// </summary>
    private PdfIntegerSize ComputeOutputSize(in PdfMatrix ctm)
    {
        PdfIntegerSize sampleSize = TileInfo.ImageSize;

        if (_alphaRowSource != null)
        {
            PdfIntegerSize maskSize = _alphaRowSource.SampleSize;

            sampleSize = new PdfIntegerSize(
                Math.Max(sampleSize.Width, maskSize.Width),
                Math.Max(sampleSize.Height, maskSize.Height));
        }

        return PdfImageCommandUtilities.GetScaledSize(ctm, sampleSize) ?? sampleSize;
    }

    private void DecodeUntilProduced(int tileIndex, IPdfExecutionObserver observer)
    {
        while (_pass.IsActive)
        {
            observer?.Notify();

            PdfImageTile[]? batch = _pass.DecodeNextTiles(_alphaRowSource, observer);
            if (batch == null)
            {
                throw new InvalidOperationException($"Decoder returned null before producing tile {tileIndex}.");
            }

            var producedRequestedTile = false;

            foreach (PdfImageTile tile in batch)
            {
                // Decoder emits every column in a row; anything not pending is discarded, not applied.
                CachedTile cachedTile = _tiles[tile.TileIndex];
                if (cachedTile.IsPendingUpdate)
                {
                    cachedTile.SetTile(tile);
                }

                if (tile.TileIndex == tileIndex)
                {
                    producedRequestedTile = true;
                }
            }

            if (!HasPendingTiles())
            {
                FinishDecoding();
            }

            if (producedRequestedTile)
            {
                return;
            }
        }

        throw new InvalidOperationException($"Tile {tileIndex} was not produced by decoder {Decoder.GetType().Name}.");
    }

    /// <summary>
    /// Releases the decoder state and the buffers the current pass reconstructs through.
    /// </summary>
    private void FinishDecoding()
    {
        _pass.End();
        _alphaRowSource?.Cleanup();
    }

    private bool HasPendingTiles()
    {
        foreach (CachedTile cachedTile in _tiles)
        {
            if (cachedTile.IsPendingUpdate)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Drops tiles whose decode size has changed under the transformation of <paramref name="executionContext"/>
    /// and evicts tiles outside its region of interest.
    /// </summary>
    private void UpdateRegion(PdfCommandExecutionContext executionContext)
    {
        FinishDecoding();

        PdfMatrix totalMatrix = executionContext.Frames.TotalMatrix;
        PdfIntegerSize outputSize = ComputeOutputSize(PdfImageCommandUtilities.GetImageCtm(totalMatrix));
        if (!Equals(outputSize, _outputSize))
        {
            foreach (CachedTile cachedTile in _tiles)
            {
                cachedTile.Clear();
            }

            _outputSize = outputSize;
        }

        PdfIntegerRectangle imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(TileInfo.ImageSize, executionContext);
        _regionTiles = TileInfo.GetTileRange(imageRegion);

        for (int tileIndex = 0; tileIndex < _tiles.Length; tileIndex++)
        {
            if (!TileInfo.IsTileInRange(tileIndex, _regionTiles))
            {
                _tiles[tileIndex].Clear();
            }
        }

        _totalMatrix = totalMatrix;
        _pageRegionOfInterest = executionContext.PageRegionOfInterest;
    }

    /// <summary>
    /// Starts a pass decoding every tile inside the region of interest that has no decoded image.
    /// </summary>
    private void BeginDecoding(PdfCommandExecutionContext executionContext)
    {
        HashSet<int> tileIndexesToDecode = [];

        for (int row = _regionTiles.Top; row < _regionTiles.Bottom; row++)
        {
            for (int column = _regionTiles.Left; column < _regionTiles.Right; column++)
            {
                int tileIndex = TileInfo.GetTileIndex(column, row);
                if (!_tiles[tileIndex].HasImage)
                {
                    tileIndexesToDecode.Add(tileIndex);
                }
            }
        }

        HashSet<int>? passTileIndexes = (tileIndexesToDecode.Count == TileInfo.TotalTiles) ? null : tileIndexesToDecode;
        PdfMatrix ctm = PdfImageCommandUtilities.GetImageCtm(executionContext.Frames.TotalMatrix);
        PdfIntegerSize outputSize = ComputeOutputSize(ctm);

        _pass.Begin(ctm, outputSize, passTileIndexes, executionContext.ExecutionObserver);
        _alphaRowSource?.Initialize(outputSize, passTileIndexes, executionContext.ExecutionObserver);

        foreach (int tileIndex in tileIndexesToDecode)
        {
            _tiles[tileIndex].IsPendingUpdate = true;
        }
    }

    /// <summary>
    /// Creates the tile cache for <paramref name="pdfImage"/>, resolving its decoder and the alpha source of its mask.
    /// </summary>
    public static PdfImageTileCacheEntry Create(PdfImage pdfImage, ImageDecodingContext context, ILoggerFactory loggerFactory)
    {
        if (pdfImage == null)
        {
            throw new ArgumentNullException(nameof(pdfImage));
        }

        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        PdfImageDecoder? decoder = PdfImageDecoder.GetDecoder(pdfImage, context, loggerFactory);

        if (decoder == null)
        {
            throw new ArgumentException($"Decoder for image {pdfImage.Type} is not defined.");
        }

        PdfTileInfo tileInfo = new(new PdfIntegerSize(pdfImage.Width, pdfImage.Height), new PdfIntegerSize(context.DefaultTileSize, context.DefaultTileSize));
        MaskAlphaRowSource? alphaRowSource = MaskAlphaRowSource.Create(decoder, tileInfo, loggerFactory);

        return new PdfImageTileCacheEntry(decoder, tileInfo, loggerFactory, alphaRowSource);
    }

    private sealed class CachedTile
    {
        private PdfImageTile _tile;

        public CachedTile(int index)
        {
            Index = index;
            _tile = PdfImageTile.CreateEmpty(index);
        }

        public int Index { get; }

        public bool IsPendingUpdate { get; set; }

        public bool HasImage => _tile.Image != null;

        public PdfImageTile GetTile() => _tile;

        public void SetTile(PdfImageTile tile)
        {
            IsPendingUpdate = false;
            _tile = tile;
        }

        public void Clear()
        {
            IsPendingUpdate = false;
            _tile = PdfImageTile.CreateEmpty(Index);
        }
    }
}
