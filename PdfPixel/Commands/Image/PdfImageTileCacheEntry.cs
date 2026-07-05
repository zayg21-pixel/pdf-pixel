using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Image;

internal sealed class PdfImageTileCacheEntry : IDisposable
{
    private readonly PdfImageDecoder _decoder;
    private readonly ImageDecodingContext _context;
    private readonly CachedTile[] _tiles;

    private PdfCommandImageCache? _imageCache;
    private SKMatrix _currentCtm;
    private int _initializeTileIndex;
    private int _getTileIndex;
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

    /// <summary>
    /// Resets tile iteration indices to the beginning without re-running decode logic.
    /// Used when the same recording is replayed multiple times (e.g. Type3 font glyphs).
    /// </summary>
    public void ResetTileIndexes()
    {
        _initializeTileIndex = 0;
        _getTileIndex = 0;
    }

    public void Initialize(SKMatrix ctm, SKRectI imageRegion, object contentLocker, IPdfExecutionObserver observer, PdfCommandImageCache? imageCache)
    {
        if (_decoding)
        {
            _decoder.Cleanup();
            _decoding = false;
        }

        bool imageCacheChanged = !ReferenceEquals(_imageCache, imageCache);
        _imageCache = imageCache;
        _currentCtm = ctm;
        _initializeTileIndex = 0;
        _getTileIndex = 0;

        // TODO: [MEDIUM] Type3 fonts share recordings across pages, each with its own image cache.
        // Switching cache forces re-decode, losing previously cached tiles. A global/local cache
        // split would let shared recordings keep their images across pages.
        if (imageCacheChanged)
        {
            foreach (CachedTile tile in _tiles)
            {
                tile.Clear();
            }
        }

        HashSet<int> regionTileIndexes = ComputeRegionTileIndexes(imageRegion);
        EvictTilesOutsideRegion(regionTileIndexes);

        HashSet<int>? tileIndexesToDecode = ComputeTileIndexesToDecode(ctm, regionTileIndexes, imageCache);

        if (tileIndexesToDecode == null || tileIndexesToDecode.Count > 0)
        {
            _decoder.Initialize(TileInfo, _context, contentLocker, ctm, tileIndexesToDecode, observer);
            _decoding = true;
        }
    }

    /// <summary>
    /// Decodes the next tile and stores it in the cache. Called during the Initialize pass.
    /// </summary>
    public void InitializeNextTile(IPdfExecutionObserver observer)
    {
        if (_initializeTileIndex >= TileInfo.TotalTiles)
        {
            throw new InvalidOperationException($"Tile index {_initializeTileIndex} is out of range (TotalTiles={TileInfo.TotalTiles}).");
        }

        CachedTile cachedTile = _tiles[_initializeTileIndex];

        if (!cachedTile.IsPendingUpdate)
        {
            _initializeTileIndex++;
            return;
        }

        while (_decoding)
        {
            observer?.Notify();

            PdfImageTile[]? batch = _decoder.DecodeNextTiles(observer);
            if (batch == null)
            {
                throw new InvalidOperationException($"Decoder returned null before producing tile {_initializeTileIndex}.");
            }

            var producedCurrentTile = false;

            foreach (PdfImageTile tile in batch)
            {
                if (_tiles[tile.TileIndex].IsPendingUpdate)
                {
                    _tiles[tile.TileIndex].UpdateTile(tile, _currentCtm, _imageCache);
                }
                else
                {
                    tile.Dispose();
                }

                if (tile.TileIndex == _initializeTileIndex)
                {
                    producedCurrentTile = true;
                }

                if (tile.TileIndex == TileInfo.TotalTiles - 1)
                {
                    _decoding = false;
                    _decoder.Cleanup();
                }
            }

            if (producedCurrentTile)
            {
                _initializeTileIndex++;
                return;
            }
        }

        throw new InvalidOperationException($"Tile {_initializeTileIndex} was not produced by decoder {_decoder.GetType().Name}.");
    }

    /// <summary>
    /// Returns the next tile from the cache. Called during the Execute pass.
    /// All tiles must have been populated by prior <see cref="InitializeNextTile"/> calls.
    /// </summary>
    public PdfImageTile GetNextTile()
    {
        if (_getTileIndex >= TileInfo.TotalTiles)
        {
            throw new InvalidOperationException($"Tile index {_getTileIndex} is out of range (TotalTiles={TileInfo.TotalTiles}).");
        }

        CachedTile cachedTile = _tiles[_getTileIndex];

        if (!cachedTile.HasTile)
        {
            throw new InvalidOperationException($"Tile at index {_getTileIndex} was not initialized.");
        }

        _getTileIndex++;
        return cachedTile.GetTile(_imageCache);
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
    private HashSet<int>? ComputeTileIndexesToDecode(SKMatrix ctm, HashSet<int> regionTileIndexes, PdfCommandImageCache? imageCache)
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

                if (tile.HasTile)
                {
                    tile.ClearPending();
                }
                else
                {
                    SKRectI position = TileInfo.GetTilePosition(tile.Index);
                    tile.UpdateTile(new PdfImageTile(tile.Index, position, null, null, isSkipped: true), ctm, imageCache);
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
    /// <see cref="ImageDecodingContext.MaxTileCacheSizeBytes"/>. Tiles inside the region are always kept, whether or not
    /// they are still valid for <see cref="_currentCtm"/>, since <see cref="ComputeTileIndexesToDecode"/>
    /// already arranges for stale ones to be replaced by the upcoming decode pass.
    /// </summary>
    private void EvictTilesOutsideRegion(HashSet<int> regionTileIndexes)
    {
        long cacheSizeBytes = ComputeCacheSizeBytes();

        for (int tileIndex = 0; tileIndex < _tiles.Length && cacheSizeBytes > _context.MaxTileCacheSizeBytes; tileIndex++)
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
    /// When a <see cref="PdfCommandImageCache"/> is available, image data is stored in the atlas
    /// and the tile itself only keeps parameters. Without a cache, the tile holds the
    /// <see cref="PdfImageTile"/> directly.
    /// </summary>
    private sealed class CachedTile : IDisposable
    {
        private readonly Guid _cacheId = Guid.NewGuid();

        private PdfCommandImageCache? _imageCache;
        private PdfImageTile? _directTile;
        private SKRectI _tilePosition;
        private PdfImageRowDecodingParameters? _parameters;
        private SKMatrix _decodedCtm;
        private bool _hasImage;
        private bool _isSkipped;
        private bool _isCached;
        private int _imageWidth;
        private int _imageHeight;

        public CachedTile(int index)
        {
            Index = index;
            IsPendingUpdate = true;
        }

        public int Index { get; }

        public bool IsPendingUpdate { get; private set; }

        public bool HasTile => _hasImage || _isSkipped;

        /// <summary>
        /// Estimated pixel memory retained by this tile's decoded image, whether stored directly
        /// or via a <see cref="PdfCommandImageCache"/> (atlas-packed or standalone). Estimated as
        /// Width * Height * 4 (RGBA8888), computed from the dimensions captured at decode time
        /// since atlas-packed images are no longer directly reachable once cached.
        /// </summary>
        public long EstimatedByteSize => _hasImage ? (long)_imageWidth * _imageHeight * 4 : 0;

        public PdfImageTile GetTile(PdfCommandImageCache? imageCache)
        {
            if (_isSkipped)
            {
                return new PdfImageTile(Index, _tilePosition, null, null, isSkipped: true);
            }

            if (_isCached && imageCache != null)
            {
                CachedImageResult cached = imageCache.GetImage(in _cacheId);

                if (cached.IsAtlased)
                {
                    return new PdfImageTile(Index, _tilePosition, cached.Image, _parameters, isSkipped: false, cached.SourceRegion);
                }

                return new PdfImageTile(Index, _tilePosition, cached.Image, _parameters, isSkipped: false);
            }

            if (_directTile == null)
            {
                throw new InvalidOperationException($"Tile at index {Index} has no image data.");
            }

            return _directTile;
        }

        public void UpdateTile(PdfImageTile tile, SKMatrix ctm, PdfCommandImageCache? imageCache)
        {
            IsPendingUpdate = false;
            _tilePosition = tile.TilePosition;
            _parameters = tile.Parameters;
            _isSkipped = tile.IsSkipped;
            _decodedCtm = ctm;
            _imageCache = imageCache;

            RemoveFromCache();
            _directTile?.Dispose();
            _directTile = null;
            _isCached = false;
            _hasImage = false;

            if (tile.Image != null)
            {
                _hasImage = true;
                _imageWidth = tile.Image.Width;
                _imageHeight = tile.Image.Height;

                if (imageCache != null)
                {
                    _isCached = true;
                    imageCache.CacheImage(in _cacheId, tile.Image);
                }
                else
                {
                    _directTile = tile;
                }
            }
            else
            {
                tile.Dispose();
            }
        }

        /// <summary>
        /// True when this entry holds actually decoded (non-skipped) pixel data produced for a
        /// transform whose linear part — scale and skew, which together determine the resolution
        /// at which the source image is sampled, see <see cref="PdfImageCommandUtilities.GetScaledSize"/> —
        /// matches <paramref name="ctm"/>.
        /// </summary>
        public bool IsValid(SKMatrix ctm)
        {
            return _hasImage
                && !IsPendingUpdate
                && _decodedCtm.ScaleX == ctm.ScaleX
                && _decodedCtm.ScaleY == ctm.ScaleY
                && _decodedCtm.SkewX == ctm.SkewX
                && _decodedCtm.SkewY == ctm.SkewY;
        }

        public void Clear()
        {
            RemoveFromCache();
            _directTile?.Dispose();
            _directTile = null;
            _hasImage = false;
            _isSkipped = false;
            _isCached = false;
            _parameters = null;
            IsPendingUpdate = true;
            _decodedCtm = SKMatrix.Empty;
        }

        public void SetAsPending() => IsPendingUpdate = true;

        public void ClearPending() => IsPendingUpdate = false;

        public void Dispose()
        {
            RemoveFromCache();
            _directTile?.Dispose();
        }

        private void RemoveFromCache()
        {
            if (_isCached && _imageCache != null)
            {
                _imageCache.Remove(in _cacheId);
                _isCached = false;
            }
        }
    }
}
