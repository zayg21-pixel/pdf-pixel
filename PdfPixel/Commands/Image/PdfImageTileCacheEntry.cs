using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

internal sealed class PdfImageTileCacheEntry : IDisposable
{
    /// <summary>
    /// Upper bound on the combined estimated byte size of cached decoded tiles. Each tile is
    /// estimated as <c>Width * Height * 4</c> (RGBA8888) regardless of its actual color type.
    /// </summary>
    private const long MaxTileCacheSizeBytes = 10 * 1024 * 1024;

    private readonly PdfImageDecoder _decoder;
    private readonly ImageDecodingContext _context;
    private readonly CachedTile[] _tiles;

    private SKMatrix _currentCtm;
    private int _currentTileIndex;
    private bool _decoding;

    public PdfImageTileCacheEntry(PdfImageDecoder decoder, ImageDecodingContext context, PdfTileInfo tileInfo)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        TileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));

        _tiles = new CachedTile[tileInfo.TotalTiles];
        for (int tileIndex = 0; tileIndex < _tiles.Length; tileIndex++)
        {
            _tiles[tileIndex] = new CachedTile(tileIndex);
        }
    }

    public PdfTileInfo TileInfo { get; }

    public void Initialize(SKMatrix ctm, SKRectI imageRegion, object contentLocker, IPdfExecutionObserver observer)
    {
        if (_decoding)
        {
            _decoder.Cleanup();
            _decoding = false;
        }

        _currentCtm = ctm;
        _currentTileIndex = 0;

        HashSet<int> regionTileIndexes = ComputeRegionTileIndexes(imageRegion);
        EvictTilesOutsideRegion(regionTileIndexes);

        HashSet<int>? tileIndexesToDecode = ComputeTileIndexesToDecode(ctm, regionTileIndexes);

        if (tileIndexesToDecode == null || tileIndexesToDecode.Count > 0)
        {
            _decoder.Initialize(TileInfo, _context, contentLocker, ctm, tileIndexesToDecode, observer);
            _decoding = true;
        }
    }

    public PdfImageTile GetNextTile(IPdfExecutionObserver observer)
    {
        if (_currentTileIndex >= TileInfo.TotalTiles)
        {
            throw new InvalidOperationException($"Tile index {_currentTileIndex} is out of range (TotalTiles={TileInfo.TotalTiles}).");
        }

        CachedTile cachedTile = _tiles[_currentTileIndex];

        if (!cachedTile.IsPendingUpdate)
        {
            PdfImageTile? tile = cachedTile.Tile;

            if (tile == null)
            {
                throw new InvalidOperationException($"Current tile at index {_currentTileIndex} is not defined.");
            }

            _currentTileIndex++;
            return tile;
        }

        while (_decoding)
        {
            observer?.Notify();

            PdfImageTile[]? batch = _decoder.DecodeNextTiles(observer);
            if (batch == null)
            {
                throw new InvalidOperationException($"Decoder returned null before producing tile {_currentTileIndex}.");
            }

            PdfImageTile? producedCurrentTile = null;

            foreach (PdfImageTile tile in batch)
            {
                if (_tiles[tile.TileIndex].IsPendingUpdate)
                {
                    _tiles[tile.TileIndex].UpdateTile(tile, _currentCtm);
                }
                else
                {
                    tile.Dispose();
                }

                if (tile.TileIndex == _currentTileIndex)
                {
                    producedCurrentTile = tile;
                }

                if (tile.TileIndex == TileInfo.TotalTiles - 1)
                {
                    _decoding = false;
                    _decoder.Cleanup();
                }
            }

            if (producedCurrentTile != null)
            {
                _currentTileIndex++;
                return producedCurrentTile;
            }
        }

        int pendingCount = 0;
        foreach (CachedTile cached in _tiles)
        {
            if (cached.IsPendingUpdate)
            {
                pendingCount++;
            }
        }

        throw new InvalidOperationException($"Tile {_currentTileIndex} was not produced by decoder {_decoder.GetType().Name}.");
    }

    /// <summary>
    /// Collects the indexes of every tile overlapping <paramref name="imageRegion"/>, in grid
    /// coordinates. Empty when the region is empty.
    /// </summary>
    private HashSet<int> ComputeRegionTileIndexes(SKRectI imageRegion)
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

    /// <summary>
    /// Collects the indexes of tiles in <paramref name="regionTileIndexes"/> that are not yet
    /// cached with currently-valid decoded pixel data for <paramref name="ctm"/> — see
    /// <see cref="CachedTile.IsValid"/>. Reads the cache directly rather than tracking a separate
    /// "decoded region" value, so the answer can never drift out of sync with what is actually
    /// cached. Returns null when every tile needs decoding, signaling the decoder to decode the
    /// whole image rather than build the full index set.
    /// </summary>
    private HashSet<int>? ComputeTileIndexesToDecode(SKMatrix ctm, HashSet<int> regionTileIndexes)
    {
        HashSet<int> tileIndexesToDecode = [];

        foreach (CachedTile tile in _tiles)
        {
            if (regionTileIndexes.Contains(tile.Index) && !tile.IsValid(ctm))
            {
                tile.SetAsPending();
                tileIndexesToDecode.Add(tile.Index);
            }
        }

        if (tileIndexesToDecode.Count == 0)
        {
            foreach (CachedTile tile in _tiles)
            {
                if (!tile.IsPendingUpdate)
                {
                    continue;
                }

                if (tile.Tile != null)
                {
                    tile.ClearPending();
                }
                else
                {
                    SKRectI position = TileInfo.GetTilePosition(tile.Index);
                    tile.UpdateTile(new PdfImageTile(tile.Index, position, null, null, isSkipped: true), ctm);
                }
            }

            return tileIndexesToDecode;
        }

        foreach (CachedTile tile in _tiles)
        {
            if (!regionTileIndexes.Contains(tile.Index))
            {
                tile.SetAsPending();
            }
        }

        return (tileIndexesToDecode.Count == TileInfo.TotalTiles) ? null : tileIndexesToDecode;
    }

    /// <summary>
    /// Drops cached tiles that lie outside <paramref name="regionTileIndexes"/> — i.e. outside the
    /// current viewport — but only while the estimated combined cache size exceeds
    /// <see cref="MaxTileCacheSizeBytes"/>. Tiles inside the region are always kept, whether or not
    /// they are still valid for <see cref="_currentCtm"/>, since <see cref="ComputeTileIndexesToDecode"/>
    /// already arranges for stale ones to be replaced by the upcoming decode pass.
    /// </summary>
    private void EvictTilesOutsideRegion(HashSet<int> regionTileIndexes)
    {
        long cacheSizeBytes = ComputeCacheSizeBytes();

        for (int tileIndex = 0; tileIndex < _tiles.Length && cacheSizeBytes > MaxTileCacheSizeBytes; tileIndex++)
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

    public void Dispose()
    {
        _decoder.Dispose();
        foreach (CachedTile cachedTile in _tiles)
        {
            cachedTile.Dispose();
        }
    }

    /// <summary>
    /// Holds a single cached tile together with the CTM it was decoded for, so the cache can
    /// tell whether the tile is still usable for a given transform without re-decoding it.
    /// </summary>
    private sealed class CachedTile : IDisposable
    {
        private PdfImageTile? _tile;
        private SKMatrix _decodedCtm;

        public CachedTile(int index) => Index = index;

        public PdfImageTile? Tile => _tile;

        public int Index { get; }

        public bool IsPendingUpdate { get; private set; }

        public long EstimatedByteSize => (_tile?.Image is SKImage image) ? ComputeImageByteSize(image) : 0;

        public void UpdateTile(PdfImageTile tile, SKMatrix ctm)
        {
            IsPendingUpdate = false;
            _tile?.Dispose();
            _tile = tile;
            _decodedCtm = ctm;
        }

        /// <summary>
        /// True when this entry holds actually decoded (non-skipped) pixel data produced for a
        /// transform whose linear part — scale and skew, which together determine the resolution
        /// at which the source image is sampled, see <see cref="PdfImageCommandUtilities.GetScaledSize"/> —
        /// matches <paramref name="ctm"/>.
        /// </summary>
        public bool IsValid(SKMatrix ctm)
        {
            return _tile != null
                && !_tile.IsSkipped
                && !IsPendingUpdate
                && _decodedCtm.ScaleX == ctm.ScaleX
                && _decodedCtm.ScaleY == ctm.ScaleY
                && _decodedCtm.SkewX == ctm.SkewX
                && _decodedCtm.SkewY == ctm.SkewY;
        }

        public void Clear()
        {
            _tile?.Dispose();
            _tile = null;
            IsPendingUpdate = true;
            _decodedCtm = SKMatrix.Empty;
        }

        public void SetAsPending() => IsPendingUpdate = true;

        public void ClearPending() => IsPendingUpdate = false;

        public void Dispose() => _tile?.Dispose();

        private static long ComputeImageByteSize(SKImage image) => (long)image.Width * image.Height * 4;
    }
}
