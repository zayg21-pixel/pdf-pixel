using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Jbig2.Decoding;
using PdfPixel.Imaging.Jbig2.Model;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.Imaging.Decoding;

internal sealed class Jbig2ImageDecoder : PdfImageDecoder
{
    private Jbig2Bitmap _cachedBitmap;
    private readonly ReadOnlyMemory<byte> _imageData;

    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public Jbig2ImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
    {
        _imageData = image.GetImageData();
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, SKMatrix ctm, SKRectI regionOfInterest)
    {
        if (!ValidateImageParameters())
            throw new InvalidOperationException($"JBIG2 image parameters are invalid (Name={Image.Name}).");
        if (_imageData.IsEmpty)
            throw new InvalidOperationException($"JBIG2 image data is empty (Name={Image.Name}).");

        EnsureBitmapDecoded(CancellationToken.None);
        if (_cachedBitmap == null)
            throw new InvalidOperationException($"JBIG2 page decoding failed (Name={Image.Name}).");

        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        _fullWidthRowBuffer = new byte[_cachedBitmap.Stride];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(CancellationToken cancellationToken = default)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            _cachedBitmap.GetRowReadOnly(_currentImageRow).CopyTo(_fullWidthRowBuffer);
            InvertRow(_fullWidthRowBuffer);
            var tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, cancellationToken);
            _currentImageRow++;
            cancellationToken.ThrowIfCancellationRequested();
            if (tiles != null) return tiles;
        }
        return null;
    }

    public override void Cleanup()
    {
        _fullWidthRowBuffer = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }

    private void EnsureBitmapDecoded(CancellationToken cancellationToken)
    {
        if (_cachedBitmap != null) return;
        Jbig2SegmentCache globalCache = ResolveGlobalsCache();
        var pageDecoder = new Jbig2PageDecoder(Logger);
        _cachedBitmap = pageDecoder.Decode(_imageData.Span, Image.Width, Image.Height, globalCache, cancellationToken);
    }

    private static void InvertRow(byte[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = (byte)~buffer[i];
    }

    private Jbig2SegmentCache ResolveGlobalsCache()
    {
        var globalsObject = Image.DecodeParms?.Jbig2Globals;
        if (globalsObject == null) return null;

        var objectCache = Image.SourceObject?.Document?.ObjectCache;
        if (objectCache != null && objectCache.Jbig2GlobalCaches.TryGetValue(globalsObject.Reference, out var existing))
            return existing;

        ReadOnlyMemory<byte> globalsData;
        try { globalsData = globalsObject.DecodeAsMemory(); }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to decode JBIG2Globals stream.");
            return null;
        }

        if (globalsData.IsEmpty) return null;

        var pageDecoder = new Jbig2PageDecoder(Logger);
        Jbig2SegmentCache globalsCache = pageDecoder.DecodeGlobalCache(globalsData.Span);

        if (objectCache != null)
            objectCache.Jbig2GlobalCaches[globalsObject.Reference] = globalsCache;

        return globalsCache;
    }
}
