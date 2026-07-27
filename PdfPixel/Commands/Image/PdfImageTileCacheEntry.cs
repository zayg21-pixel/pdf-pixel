using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Caches decoded tiles for one PDF image across repeated renders (e.g. scrolling). <see cref="Initialize"/>
/// marks in-viewport tiles without an image as pending; <see cref="GetNextTile"/> then decodes only those,
/// in order. Tiles outside the viewport are left alone. A CTM change that alters the decode size
/// (images are never upscaled) drops the whole cache, since every tile shares one decode size.
/// </summary>
public sealed class PdfImageTileCacheEntry
{
    private readonly CachedTile[] _tiles;

    private PdfIntegerSize? _scaledSize;
    private int _tileIndex;
    private bool _decoding;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageTileCacheEntry"/> class.
    /// </summary>
    public PdfImageTileCacheEntry(PdfImageDecoder decoder, PdfTileInfo tileInfo)
    {
        Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        TileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));

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
        if (_decoding)
        {
            Decoder.Cleanup();
            _decoding = false;
        }

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
            Decoder.Initialize(TileInfo, contentLocker, ctm, tileIndexesToDecode, observer);
            _decoding = true;
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

        if (!_decoding)
        {
            return cachedTile.GetTile();
        }

        if (cachedTile.IsPendingUpdate)
        {
            DecodeUntilProduced(tileIndex, observer);
        }

        return cachedTile.GetTile();
    }

    private void DecodeUntilProduced(int tileIndex, IPdfExecutionObserver observer)
    {
        while (_decoding)
        {
            observer?.Notify();

            PdfImageTile[]? batch = Decoder.DecodeNextTiles(observer);
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
                    _decoding = false;
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
        long cacheSizeBytes = ComputeCacheSizeBytes();

        for (int tileIndex = 0; tileIndex < _tiles.Length && cacheSizeBytes > Decoder.Context.MaxTileCacheSizeBytes; tileIndex++)
        {
            if (regionTileIndexes.Contains(tileIndex))
            {
                continue;
            }

            CachedTile cachedTile = _tiles[tileIndex];
            long estimatedByteSize = cachedTile.EstimatedByteSize;
            if (estimatedByteSize == 0)
            {
                continue;
            }

            cachedTile.Clear();
            cacheSizeBytes -= estimatedByteSize;
        }
    }

    private long ComputeCacheSizeBytes()
    {
        long cacheSizeBytes = 0;

        foreach (CachedTile cachedTile in _tiles)
        {
            cacheSizeBytes += cachedTile.EstimatedByteSize;
        }

        return cacheSizeBytes;
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

        public long EstimatedByteSize => (_tile.Image != null) ? (long)_tile.Image.Width * _tile.Image.Height * 4 : 0;

        public PdfImageTile GetTile() => _tile;

        public void SetTile(PdfImageTile tile)
        {
            IsPendingUpdate = false;
            _tile = tile;
        }

        public void Clear() => _tile = PdfImageTile.CreateEmpty(Index);
    }
}
