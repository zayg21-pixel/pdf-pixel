using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Jbig2.Model;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal sealed class Jbig2ImageDecoder : PdfImageDecoder
{
    private Jbig2Bitmap _cachedBitmap;

    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public Jbig2ImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
    {
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, SKRectI regionOfInterest, IPdfExecutionObserver observer)
    {
        if (!ValidateImageParameters())
            throw new InvalidOperationException($"JBIG2 image parameters are invalid (Name={Image.Name}).");

        EnsureBitmapDecoded(contentLocker, observer);
        if (_cachedBitmap == null)
            throw new InvalidOperationException($"JBIG2 page decoding failed (Name={Image.Name}).");

        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        _fullWidthRowBuffer = new byte[_cachedBitmap.Stride];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(IPdfExecutionObserver observer)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            _cachedBitmap.GetRowReadOnly(_currentImageRow).CopyTo(_fullWidthRowBuffer);
            InvertRow(_fullWidthRowBuffer);
            var tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, observer);
            _currentImageRow++;
            observer?.Notify();
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

    private void EnsureBitmapDecoded(object contentLocker, IPdfExecutionObserver observer)
    {
        if (_cachedBitmap != null)
        {
            return;
        }

        ReadOnlyMemory<byte> imageData;

        lock (contentLocker)
        {
            imageData = Image.GetImageData(observer);
        }

        if (imageData.IsEmpty)
        {
            throw new InvalidOperationException($"JBIG2 image data is empty (Name={Image.Name}).");
        }

        Jbig2SegmentCache globalCache = ResolveGlobalsCache(contentLocker);
        var pageDecoder = new Jbig2PageDecoder();
        var jbig2Observer = new Jbig2Observer(observer);
        _cachedBitmap = pageDecoder.Decode(imageData.Span, Image.Width, Image.Height, globalCache, jbig2Observer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvertRow(byte[] buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)~buffer[i];
        }
    }

    private Jbig2SegmentCache ResolveGlobalsCache(object contentLocker)
    {
        var globalsObject = Image.DecodeParms?.Jbig2Globals;
        if (globalsObject == null) return null;

        var objectCache = Image.SourceObject?.Document?.ObjectCache;
        if (objectCache != null && objectCache.Jbig2GlobalCaches.TryGetValue(globalsObject.Reference, out var existing))
            return existing;

        ReadOnlyMemory<byte> globalsData;
        try
        {
            lock (contentLocker)
            {
                globalsData = globalsObject.DecodeAsMemory();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to decode JBIG2Globals stream.");
            return null;
        }

        if (globalsData.IsEmpty) return null;

        var pageDecoder = new Jbig2PageDecoder();
        Jbig2SegmentCache globalsCache = pageDecoder.DecodeGlobalCache(globalsData.Span);

        if (objectCache != null)
            objectCache.Jbig2GlobalCaches[globalsObject.Reference] = globalsCache;

        return globalsCache;
    }

    private sealed class Jbig2Observer : IJBig2ExectionObserver
    {
        private readonly IPdfExecutionObserver _pdfObserver;
        public Jbig2Observer(IPdfExecutionObserver pdfObserver)
        {
            _pdfObserver = pdfObserver;
        }

        public void Notify() => _pdfObserver?.Notify();
    }
}
