using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jbig2;
using PdfPixel.Objects;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Decoding;

internal sealed class Jbig2ImageDecoder : PdfImageDecoder
{
    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private ReadOnlyMemory<byte>? _samples;
    private int _rowStride;
    private PdfImageSampleInfo? _sampleInfo;

    public Jbig2ImageDecoder(PdfImage image)
        : base(image)
    {
        _colorSpaceConverter = image.ColorSpace
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
    }

    protected override PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfExecutionObserver? observer)
    {
        PdfImageSampleInfo sampleInfo = EnsureSampleInfo();

        _samples = Cache.Find(Image, WholeImageKey);

        if (_samples == null)
        {
            _samples = DecodeSamples(observer);
        }

        return new PdfImageDecodeInfo(sampleInfo, null);
    }

    public override PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer)
    {
        if (_sampleInfo == null || _samples == null)
        {
            throw new InvalidOperationException("No tiles are selected.");
        }

        return CreateBitmapTile(_sampleInfo, _samples.Value, _rowStride, tileIndex);
    }

    protected override void DropTiles() => _samples = null;

    private PdfImageSampleInfo EnsureSampleInfo()
    {
        if (_sampleInfo == null)
        {
            _sampleInfo = new PdfImageSampleInfo(CreateGrid(new PdfIntegerSize(Image.Width, Image.Height)), descaleFactor: 1, Image.BitsPerComponent, 1, _colorSpaceConverter);
        }

        return _sampleInfo;
    }

    /// <summary>
    /// Decodes the page and puts its rows, every bit inverted, into the cache.
    /// </summary>
    private byte[] DecodeSamples(IPdfExecutionObserver? observer)
    {
        Jbig2Bitmap bitmap = DecodeBitmap(observer);
        byte[] samples = bitmap.ReadOnlyData.ToArray();

        Invert(samples);

        _rowStride = bitmap.Stride;
        Cache.Put(Image, WholeImageKey, samples);

        return samples;
    }

    private Jbig2Bitmap DecodeBitmap(IPdfExecutionObserver? observer)
    {
        ReadOnlyMemory<byte> imageData = Image.Stream.DecodeAsMemory(observer);

        if (imageData.IsEmpty)
        {
            throw new InvalidOperationException($"JBIG2 image data is empty (Reference={Image.Reference}).");
        }

        Jbig2SegmentCache? globalCache = ResolveGlobalsCache();
        Jbig2PageDecoder pageDecoder = new();
        Jbig2Observer jbig2Observer = new(observer);
        Jbig2Bitmap? bitmap = pageDecoder.Decode(imageData.Span, Image.Width, Image.Height, globalCache, jbig2Observer);

        if (bitmap == null)
        {
            throw new InvalidOperationException($"JBIG2 page decoding failed (Reference={Image.Reference}).");
        }

        return bitmap;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Invert(in Span<byte> buffer)
    {
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)~buffer[i];
        }
    }

    private Jbig2SegmentCache? ResolveGlobalsCache()
    {
        var decodeParameters = Image.DecodeParameters as PdfJbig2DecodeParameters;
        if (decodeParameters == null)
        {
            return null;
        }

        return Image.Document.ObjectCache.GetOrCreate(decodeParameters.Jbig2GlobalsReference, DecodeGlobalsCache, requiresStream: true);
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

    private sealed class Jbig2Observer : IJbig2ExecutionObserver
    {
        private readonly IPdfExecutionObserver? _pdfObserver;

        public Jbig2Observer(IPdfExecutionObserver? pdfObserver) => _pdfObserver = pdfObserver;

        public void Notify() => _pdfObserver?.Notify();
    }
}
