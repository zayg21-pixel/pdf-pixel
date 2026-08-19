using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Caches decoded tiles for one PDF image across repeated renders (e.g. scrolling). <see cref="Initialize"/>
/// </summary>
public sealed class PdfImageTileCacheEntry
{
    private readonly CachedTile[] _tiles;
    private readonly ILoggerFactory _loggerFactory;

    private PdfIntegerSize? _scaledSize;
    private int _tileIndex;
    private bool _decoderActive;

    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private byte[]? _rowBuffer;
    private int _currentImageRow;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageTileCacheEntry"/> class.
    /// </summary>
    public PdfImageTileCacheEntry(PdfImageDecoder decoder, PdfTileInfo tileInfo, ILoggerFactory loggerFactory)
    {
        Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        TileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));

        _tiles = new CachedTile[tileInfo.TotalTiles];
        for (int tileIndex = 0; tileIndex < _tiles.Length; tileIndex++)
        {
            _tiles[tileIndex] = new CachedTile(tileIndex);
        }
    }

    /// <summary>
    /// Decoder used to produce tiles for this cache entry.
    /// </summary>
    public PdfImageDecoder Decoder { get; }

    /// <summary>
    /// Gets the tiling layout shared by every tile in this cache.
    /// </summary>
    public PdfTileInfo TileInfo { get; }

    /// <summary>
    /// Marks in-viewport tiles without a decoded image as pending and starts decoding them, dropping
    /// tiles whose decode size has changed and evicting tiles outside <paramref name="imageRegion"/>.
    /// </summary>
    public void Initialize(in PdfMatrix ctm, in PdfIntegerRectangle imageRegion, object contentLocker, IPdfExecutionObserver observer)
    {
        if (_decoderActive)
        {
            Decoder.Cleanup();
            _decoderActive = false;
        }

        _tilingContext = null;
        _imageParameters = null;
        _rowBuffer = null;
        _currentImageRow = 0;
        _tileIndex = 0;

        PdfIntegerSize? scaledSize = PdfImageCommandUtilities.GetScaledSize(ctm, TileInfo.ImageSize);
        if (!Equals(scaledSize, _scaledSize))
        {
            foreach (CachedTile cachedTile in _tiles)
            {
                cachedTile.Clear();
            }

            _scaledSize = scaledSize;
        }

        HashSet<int> regionTileIndexes = ComputeRegionTileIndexes(imageRegion);
        EvictTilesOutsideRegion(regionTileIndexes);

        HashSet<int>? tileIndexesToDecode = MarkTileIndexesToDecode(regionTileIndexes);

        if (tileIndexesToDecode == null || tileIndexesToDecode.Count > 0)
        {
            _imageParameters = Decoder.Initialize(ComputeRegionsOfInterest(tileIndexesToDecode), contentLocker, ctm, observer);
            _tilingContext = new PdfImageTilingContext(TileInfo, _imageParameters, tileIndexesToDecode, _loggerFactory);
            _rowBuffer = new byte[_imageParameters.RowBytes];
            _decoderActive = true;
        }
    }

    /// <summary>
    /// Returns the next tile in iteration order, decoding it first if it is pending an update.
    /// </summary>
    public PdfImageTile GetNextTile(IPdfExecutionObserver observer)
    {
        if (_tileIndex >= TileInfo.TotalTiles)
        {
            throw new InvalidOperationException($"Tile index {_tileIndex} is out of range (TotalTiles={TileInfo.TotalTiles}).");
        }

        int tileIndex = _tileIndex++;
        CachedTile cachedTile = _tiles[tileIndex];

        if (_decoderActive && cachedTile.IsPendingUpdate)
        {
            DecodeUntilProduced(tileIndex, observer);
        }

        PdfImageTile tile = cachedTile.GetTile();

        if (!Decoder.Context.CacheDecodedTiles)
        {
            cachedTile.Clear();
        }

        return tile;
    }

    /// <summary>
    /// Reads decoded rows into the tiling context until a tile row completes, and returns the tiles
    /// it produced. Returns null once the decoder stops before the grid is complete.
    /// </summary>
    private PdfImageTile[]? DecodeNextTiles(IPdfExecutionObserver? observer)
    {
        if (_imageParameters == null || _tilingContext == null || _rowBuffer == null)
        {
            return null;
        }

        while (_currentImageRow < _imageParameters.Height)
        {
            if (!Decoder.TryReadNextRow(_rowBuffer, observer))
            {
                return null;
            }

            PdfImageTile[]? tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _rowBuffer, default, observer);
            _currentImageRow++;
            observer?.Notify();

            if (tiles != null)
            {
                return tiles;
            }
        }

        return null;
    }

    private List<PdfIntegerRectangle>? ComputeRegionsOfInterest(HashSet<int>? tileIndexesToDecode)
    {
        if (tileIndexesToDecode == null)
        {
            return null;
        }

        List<PdfIntegerRectangle> regionsOfInterest = new(tileIndexesToDecode.Count);
        foreach (int tileIndex in tileIndexesToDecode)
        {
            regionsOfInterest.Add(TileInfo.GetTilePosition(tileIndex));
        }

        return regionsOfInterest;
    }

    private void DecodeUntilProduced(int tileIndex, IPdfExecutionObserver observer)
    {
        while (_decoderActive)
        {
            observer?.Notify();

            PdfImageTile[]? batch = DecodeNextTiles(observer);
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

                if (tile.TileIndex == TileInfo.TotalTiles - 1)
                {
                    _decoderActive = false;
                    Decoder.Cleanup();
                }
            }

            if (producedRequestedTile)
            {
                return;
            }
        }

        throw new InvalidOperationException($"Tile {tileIndex} was not produced by decoder {Decoder.GetType().Name}.");
    }

    private HashSet<int> ComputeRegionTileIndexes(in PdfIntegerRectangle imageRegion)
    {
        HashSet<int> regionTileIndexes = [];

        if (!imageRegion.IsEmpty)
        {
            int columnStart = imageRegion.Left / TileInfo.TileWidth;
            int columnEnd = (imageRegion.Right - 1) / TileInfo.TileWidth;
            int rowStart = imageRegion.Top / TileInfo.TileHeight;
            int rowEnd = (imageRegion.Bottom - 1) / TileInfo.TileHeight;

            for (int row = rowStart; row <= rowEnd; row++)
            {
                for (int column = columnStart; column <= columnEnd; column++)
                {
                    regionTileIndexes.Add((row * TileInfo.TilesHorizontal) + column);
                }
            }
        }

        return regionTileIndexes;
    }

    private HashSet<int>? MarkTileIndexesToDecode(HashSet<int> regionTileIndexes)
    {
        HashSet<int> tileIndexesToDecode = [];

        foreach (int tileIndex in regionTileIndexes)
        {
            CachedTile cachedTile = _tiles[tileIndex];
            if (!cachedTile.HasImage)
            {
                cachedTile.IsPendingUpdate = true;
                tileIndexesToDecode.Add(tileIndex);
            }
        }

        return (tileIndexesToDecode.Count == TileInfo.TotalTiles) ? null : tileIndexesToDecode;
    }

    private void EvictTilesOutsideRegion(HashSet<int> regionTileIndexes)
    {
        for (int tileIndex = 0; tileIndex < _tiles.Length; tileIndex++)
        {
            if (!regionTileIndexes.Contains(tileIndex))
            {
                _tiles[tileIndex].Clear();
            }
        }
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

        public void Clear() => _tile = PdfImageTile.CreateEmpty(Index);
    }
}
