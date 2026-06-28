using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Jbig2.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal sealed class Jbig2ImageDecoder : PdfImageDecoder
{
    private Jbig2Bitmap? _cachedBitmap;

    private byte[]? _fullWidthRowBuffer;
    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private int _currentImageRow;

    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    public Jbig2ImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
        _colorSpaceConverter = image.ColorSpaceConverter
            ?? image.Page.Cache.ColorSpace.ResolveDeviceConverter(1)
            ?? DeviceGrayConverter.Instance;
    }

    protected override PdfColorSpaceConverter ResolvedColorSpaceConverter => _colorSpaceConverter;

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, HashSet<int>? tileIndexesToDecode, IPdfExecutionObserver? observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"JBIG2 image parameters are invalid (Name={Image.Name}).");
        }

        EnsureBitmapDecoded(contentLocker, observer);
        if (_cachedBitmap == null)
        {
            throw new InvalidOperationException($"JBIG2 page decoding failed (Name={Image.Name}).");
        }

        PdfColorSpaceConverter converter = _colorSpaceConverter;
        SKSizeI? downscaledSize = PdfImageCommandUtilities.GetScaledSize(ctm, new SKSizeI(Image.Width, Image.Height));

        _imageParameters = new PdfImageRowDecodingParameters(
            context,
            Image.Width,
            Image.Height,
            Image.BitsPerComponent,
            Image.RenderingIntent,
            converter,
            Image.HasImageMask,
            Image.MaskArray,
            Image.DecodeArray,
            downscaledSize);

        _fullWidthRowBuffer = new byte[_cachedBitmap.Stride];
        _tilingContext = new PdfImageTilingContext(tileInfo, _imageParameters, tileIndexesToDecode, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[]? DecodeNextTiles(IPdfExecutionObserver? observer)
    {
        if (_imageParameters == null || _cachedBitmap == null || _fullWidthRowBuffer == null || _tilingContext == null)
        {
            return null;
        }

        while (_currentImageRow < _imageParameters.Height)
        {
            _cachedBitmap.GetRowReadOnly(_currentImageRow).CopyTo(_fullWidthRowBuffer);
            InvertRow(_fullWidthRowBuffer);
            PdfImageTile[]? tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, observer);
            _currentImageRow++;
            observer?.Notify();
            if (tiles != null)
            {
                return tiles;
            }
        }

        return null;
    }

    private void EnsureBitmapDecoded(object contentLocker, IPdfExecutionObserver? observer)
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

        Jbig2SegmentCache? globalCache = ResolveGlobalsCache(contentLocker);
        Jbig2PageDecoder pageDecoder = new();
        Jbig2Observer jbig2Observer = new(observer);
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

    private Jbig2SegmentCache? ResolveGlobalsCache(object contentLocker)
    {
        Models.PdfObject? globalsObject = Image.DecodeParms?.Jbig2Globals;
        if (globalsObject == null)
        {
            return null;
        }

        Models.PdfDocumentObjectCache? objectCache = Image.Page.Document.ObjectCache;
        if (objectCache != null && objectCache.Jbig2GlobalCaches.TryGetValue(globalsObject.Reference, out Jbig2SegmentCache? existing))
        {
            return existing;
        }

        ReadOnlyMemory<byte> globalsData;
        lock (contentLocker)
        {
            globalsData = globalsObject.DecodeAsMemory();
        }

        if (globalsData.IsEmpty)
        {
            return null;
        }

        Jbig2PageDecoder pageDecoder = new();
        Jbig2SegmentCache globalsCache = pageDecoder.DecodeGlobalCache(globalsData.Span);

        if (objectCache != null)
        {
            objectCache.Jbig2GlobalCaches[globalsObject.Reference] = globalsCache;
        }

        return globalsCache;
    }

    private sealed class Jbig2Observer : IJBig2ExectionObserver
    {
        private readonly IPdfExecutionObserver? _pdfObserver;

        public Jbig2Observer(IPdfExecutionObserver? pdfObserver) => _pdfObserver = pdfObserver;

        public void Notify() => _pdfObserver?.Notify();
    }

    public override void Cleanup()
    {
        _fullWidthRowBuffer = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tilingContext?.Dispose();
        }
    }
}
