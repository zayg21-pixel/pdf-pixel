using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpeg;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal sealed class JpegImageDecoder : PdfRowImageDecoder
{
    private const int MaxDescaleFactor = 8;

    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private ReadOnlyMemory<byte> _encodedData;
    private JpegHeader? _header;
    private PdfImageSampleInfo? _sampleInfo;
    private int _descaleFactor;
    private IJpegDecoder? _jpegRowDecoder;

    public JpegImageDecoder(PdfImage image)
        : base(image)
    {
        _colorSpaceConverter = image.ColorSpace
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceRGB);
    }

    protected override PdfImageSampleInfo SelectSamples(in PdfIntegerSize targetSize, IPdfExecutionObserver? observer)
    {
        JpegHeader header = EnsureHeader(observer);
        PdfIntegerSize sampleSize = GetSampleSize(header);
        int descaleFactor = SelectDescaleFactor(sampleSize, targetSize, _colorSpaceConverter, MaxDescaleFactor);

        if (_sampleInfo == null || _descaleFactor != descaleFactor)
        {
            PdfIntegerSize decodedSize = new(Descale(sampleSize.Width, descaleFactor), Descale(sampleSize.Height, descaleFactor));

            _sampleInfo = new PdfImageSampleInfo(CreateGrid(decodedSize), descaleFactor, Image.BitsPerComponent, _colorSpaceConverter.Components, _colorSpaceConverter);
            _descaleFactor = descaleFactor;
        }

        return _sampleInfo;
    }

    protected override void BeginRows(PdfImageSampleInfo samples, IReadOnlyList<PdfIntegerRectangle> regions, IPdfExecutionObserver? observer)
    {
        JpegHeader header = EnsureHeader(observer);
        List<PdfIntegerRectangle> mappedRegions = MapRegionsToStoredGrid(regions, _descaleFactor);

        _jpegRowDecoder = CreateJpegDecoder(_encodedData, header, _descaleFactor, ToJpegRegions(mappedRegions));
    }

    protected override bool TryReadRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_jpegRowDecoder == null)
        {
            return false;
        }

        if (!_jpegRowDecoder.TryReadRow(destination))
        {
            throw new InvalidOperationException($"JPEG decode failed (Reference={Image.Reference}).");
        }

        return true;
    }

    protected override void EndRows() => _jpegRowDecoder = null;

    private JpegHeader EnsureHeader(IPdfExecutionObserver? observer)
    {
        if (_header != null)
        {
            return _header;
        }

        ReadOnlyMemory<byte> encodedData = Image.Stream.DecodeAsMemory(observer);

        var header = JpegHeader.Parse(encodedData.Span);

        if (header == null || header.ContentOffset < 0)
        {
            throw new InvalidOperationException($"JPEG header is invalid (Reference={Image.Reference}).");
        }

        _encodedData = encodedData;
        _header = header;

        return header;
    }

    private PdfIntegerSize GetSampleSize(JpegHeader header)
    {
        // The row count is the smaller of the two declared heights. A frame header may carry a
        // placeholder line count — that is what the DNL marker exists for — and rows past the
        // dictionary's height have no entropy-coded data behind them, so decoding up to a larger
        // SOF height yields blank rows and stretches the tile grid over them. The width stays the
        // SOF's, because it is the stride the row decoder writes and the row buffer must hold it.
        int decodedHeight = Math.Min(header.Height, Image.Height);

        return new PdfIntegerSize(header.Width, decodedHeight);
    }

    private static List<JpegRectangle>? ToJpegRegions(List<PdfIntegerRectangle>? regionsOfInterest)
    {
        if (regionsOfInterest == null)
        {
            return null;
        }

        List<JpegRectangle> jpegRegionsOfInterest = new(regionsOfInterest.Count);
        for (int index = 0; index < regionsOfInterest.Count; index++)
        {
            PdfIntegerRectangle region = regionsOfInterest[index];
            jpegRegionsOfInterest.Add(new JpegRectangle(region.Left, region.Top, region.Width, region.Height));
        }

        return jpegRegionsOfInterest;
    }

    private IJpegDecoder CreateJpegDecoder(
        in ReadOnlyMemory<byte> encodedData,
        JpegHeader header,
        int descaleFactor,
        IReadOnlyList<JpegRectangle>? regionsOfInterest)
    {
        int? colorTransform = (Image.DecodeParameters as PdfDctDecodeParameters)?.ColorTransform;

        JpegYuvMode yuvMode = colorTransform switch
        {
            0 => JpegYuvMode.NoYuv,
            1 => JpegYuvMode.ForceYuv,
            _ => JpegYuvMode.Default
        };

        JpegDecoderOptions decoderOptions = new()
        {
            YuvMode = yuvMode,
            InvertCmykColors = false,
            DescaleFactor = descaleFactor,
            RegionsOfInterest = regionsOfInterest
        };

        return JpegDecoderFactory.Create(header, encodedData, decoderOptions);
    }
}
