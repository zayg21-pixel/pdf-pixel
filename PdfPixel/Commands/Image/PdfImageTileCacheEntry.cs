using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.Commands.Image;

internal sealed class PdfImageTileCacheEntry : IDisposable
{
    private readonly PdfImageDecoder _decoder;
    private readonly ImageDecodingContext _context;
    private readonly PdfImageTile[] _tiles;

    private int _currentTileIndex;
    private SKRectI? _decodedRegion;
    private SKRectI _pendingDecodeRegion;
    private float _cachedScaleX = float.NaN;
    private float _cachedScaleY = float.NaN;
    private bool _decoding;

    public PdfImageTileCacheEntry(PdfImageDecoder decoder, ImageDecodingContext context, PdfTileInfo tileInfo)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        TileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));
        _tiles = new PdfImageTile[tileInfo.TotalTiles];
    }

    public PdfTileInfo TileInfo { get; }

    public void Initialize(SKMatrix ctm, SKRectI imageRegion)
    {
        if (ctm.ScaleX != _cachedScaleX || ctm.ScaleY != _cachedScaleY)
            InvalidateCache();

        _cachedScaleX = ctm.ScaleX;
        _cachedScaleY = ctm.ScaleY;
        _currentTileIndex = 0;

        SKRectI regionToDecode = ComputeRegionToDecode(imageRegion);

        _decoder.Initialize(TileInfo, _context, ctm, regionToDecode);
        _decoding = true;
        _pendingDecodeRegion = regionToDecode;
    }

    public PdfImageTile GetNextTile(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_currentTileIndex >= TileInfo.TotalTiles)
            throw new InvalidOperationException($"Tile index {_currentTileIndex} is out of range (TotalTiles={TileInfo.TotalTiles}).");

        if (_tiles[_currentTileIndex] != null)
            return _tiles[_currentTileIndex++];

        while (_decoding)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PdfImageTile[] batch = _decoder.DecodeNextTiles(cancellationToken);
            if (batch == null)
            {
                _decoding = false;
                _decodedRegion = _decodedRegion.HasValue
                    ? SKRectI.Union(_decodedRegion.Value, _pendingDecodeRegion)
                    : _pendingDecodeRegion;
                break;
            }
            foreach (PdfImageTile tile in batch)
            {
                if (_tiles[tile.TileIndex] == null)
                    _tiles[tile.TileIndex] = tile;
                else
                    tile.Dispose();
            }

            if (_tiles[_currentTileIndex] != null)
                return _tiles[_currentTileIndex++];
        }

        throw new InvalidOperationException($"Tile {_currentTileIndex} was not produced by the decoder.");
    }

    private SKRectI ComputeRegionToDecode(SKRectI imageRegion)
    {
        if (!_decodedRegion.HasValue) return imageRegion;

        SKRectI prev = _decodedRegion.Value;
        if (prev.Contains(imageRegion)) return SKRectI.Empty;

        bool grewRight = imageRegion.Right > prev.Right;
        bool grewDown = imageRegion.Bottom > prev.Bottom;

        if (grewRight && !grewDown) return new SKRectI(prev.Right, 0, imageRegion.Right, imageRegion.Bottom);
        if (grewDown && !grewRight) return new SKRectI(0, prev.Bottom, imageRegion.Right, imageRegion.Bottom);

        return imageRegion;
    }

    private void InvalidateCache()
    {
        if (_decoding)
        {
            _decoder.Cleanup();
            _decoding = false;
        }
        for (int i = 0; i < _tiles.Length; i++)
        {
            _tiles[i]?.Dispose();
            _tiles[i] = null;
        }
        _decodedRegion = null;
    }

    public void Dispose()
    {
        _decoder.Cleanup();
        for (int i = 0; i < _tiles.Length; i++)
        {
            _tiles[i]?.Dispose();
            _tiles[i] = null;
        }
    }
}
