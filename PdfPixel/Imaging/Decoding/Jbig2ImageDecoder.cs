using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jbig2.Decoding;
using PdfPixel.Jbig2.Model;
using PdfPixel.Objects;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal sealed class Jbig2ImageDecoder : PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private Jbig2Bitmap? _cachedBitmap;

    private int _currentImageRow;
    private int _rowCount;

    public Jbig2ImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        _colorSpaceConverter = context.ColorSpaceConverter
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        EnsureBitmapDecoded(observer);
        if (_cachedBitmap == null)
        {
            throw new InvalidOperationException($"JBIG2 page decoding failed (Reference={Image.Reference}).");
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

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
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

    private void EnsureBitmapDecoded(IPdfExecutionObserver? observer)
    {
        if (_cachedBitmap != null)
        {
            return;
        }

        ReadOnlyMemory<byte> imageData = Image.Stream.DecodeAsMemory(observer);

        if (imageData.IsEmpty)
        {
            throw new InvalidOperationException($"JBIG2 image data is empty (Reference={Image.Reference}).");
        }

        Jbig2SegmentCache? globalCache = ResolveGlobalsCache();
        Jbig2PageDecoder pageDecoder = new();
        Jbig2Observer jbig2Observer = new(observer);
        _cachedBitmap = pageDecoder.Decode(imageData.Span, Image.Width, Image.Height, globalCache, jbig2Observer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void InvertRow(in Span<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)~buffer[i];
        }
    }

    private Jbig2SegmentCache? ResolveGlobalsCache()
    {
        var decodeParameters = Image.DecodeParms as PdfJbig2DecodeParameters;
        if (decodeParameters == null)
        {
            return null;
        }

        return Context.Host.Document.ObjectCache.GetOrCreate(decodeParameters.Jbig2GlobalsReference, DecodeGlobalsCache, requiresStream: true);
    }

    private static Jbig2SegmentCache? DecodeGlobalsCache(PdfObject globalsObject)
    {
        ReadOnlyMemory<byte> globalsData = globalsObject.Stream.DecodeAsMemory();
        if (globalsData.IsEmpty)
        {
            return null;
        }

        Jbig2PageDecoder pageDecoder = new();

        return pageDecoder.DecodeGlobalCache(globalsData.Span);
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
