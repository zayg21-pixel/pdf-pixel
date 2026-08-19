using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Jbig2.Model;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal sealed class Jbig2ImageDecoder : PdfImageDecoder
{
    private Jbig2Bitmap? _cachedBitmap;

    private int _currentImageRow;
    private int _rowCount;

    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    public Jbig2ImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        _colorSpaceConverter = context.ColorSpaceConverter
            ?? context.Page.Cache.ColorSpace.ResolveDeviceConverter(1)
            ?? PdfDeviceGrayColorSpaceConverter.Instance;
    }

    protected override PdfColorSpaceConverter ResolvedColorSpaceConverter => _colorSpaceConverter;

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        object contentLocker,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"JBIG2 image parameters are invalid (SourceReference={Image.SourceReference}).");
        }

        EnsureBitmapDecoded(contentLocker, observer);
        if (_cachedBitmap == null)
        {
            throw new InvalidOperationException($"JBIG2 page decoding failed (SourceReference={Image.SourceReference}).");
        }

        PdfImageRowDecodingParameters parameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(Image.Width, Image.Height),
            Image.BitsPerComponent,
            _colorSpaceConverter);

        _currentImageRow = 0;
        _rowCount = parameters.Height;

        return parameters;
    }

    public override bool TryReadNextRow(byte[] destination, IPdfExecutionObserver? observer)
    {
        if (_cachedBitmap == null || _currentImageRow >= _rowCount)
        {
            return false;
        }

        _cachedBitmap.GetRowReadOnly(_currentImageRow).CopyTo(destination);
        InvertRow(destination);
        _currentImageRow++;

        return true;
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
            throw new InvalidOperationException($"JBIG2 image data is empty (SourceReference={Image.SourceReference}).");
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
        PdfDecodeParameters? decodeParameters = Image.DecodeParms;
        if (decodeParameters == null)
        {
            return null;
        }

        PdfObjectStream? globalsStream = decodeParameters.Jbig2Globals;
        if (globalsStream == null)
        {
            return null;
        }

        Models.PdfReference globalsReference = decodeParameters.Jbig2GlobalsReference;
        Models.PdfDocumentObjectCache? objectCache = Context.Page.Document.ObjectCache;

        if (objectCache != null && objectCache.Jbig2GlobalCaches.TryGetValue(globalsReference, out Jbig2SegmentCache? existing))
        {
            return existing;
        }

        ReadOnlyMemory<byte> globalsData;
        lock (contentLocker)
        {
            globalsData = globalsStream.DecodeAsMemory();
        }

        if (globalsData.IsEmpty)
        {
            return null;
        }

        Jbig2PageDecoder pageDecoder = new();
        Jbig2SegmentCache globalsCache = pageDecoder.DecodeGlobalCache(globalsData.Span);

        if (objectCache != null)
        {
            objectCache.Jbig2GlobalCaches[globalsReference] = globalsCache;
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
        _currentImageRow = 0;
        _rowCount = 0;
    }
}
