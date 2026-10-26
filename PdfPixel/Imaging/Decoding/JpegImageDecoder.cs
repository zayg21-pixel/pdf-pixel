using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpeg;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal sealed class JpegImageDecoder : PdfImageDecoder
{
    private const int MaxDescaleFactor = 8;

    private readonly PdfColorSpaceConverter _colorSpaceConverter;

    private IJpegDecoder? _jpegRowDecoder;

    public JpegImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        _colorSpaceConverter = context.ColorSpaceConverter
            ?? PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceRGB);
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        ReadOnlyMemory<byte> encodedData = Image.Stream.DecodeAsMemory(observer);

        var header = JpegHeader.Parse(encodedData.Span);

        if (header == null || header.ContentOffset < 0)
        {
            throw new InvalidOperationException($"JPEG header is invalid (Reference={Image.Reference}).");
        }

        PdfColorSpaceConverter resolvedConverter = _colorSpaceConverter;
        if (Context.ColorSpaceConverter == null || Context.Host.Resources.DeviceColorMapping.Map(resolvedConverter).IsDevice)
        {
            byte[]? profileBytes = header.GetIccProfile();
            if (profileBytes != null)
            {
                resolvedConverter = new PdfIccColorSpaceConverter(header.ComponentCount, resolvedConverter, profileBytes);
            }
        }

        // The row count is the smaller of the two declared heights. A frame header may carry a
        // placeholder line count — that is what the DNL marker exists for — and rows past the
        // dictionary's height have no entropy-coded data behind them, so decoding up to a larger
        // SOF height yields blank rows and stretches the tile grid over them. The width stays the
        // SOF's, because it is the stride the row decoder writes and the row buffer must hold it.
        int decodedHeight = Math.Min(header.Height, Image.Height);

        PdfIntegerSize sampleSize = new(header.Width, decodedHeight);
        int descaleFactor = ComputeDescaleFactor(sampleSize, ctm, resolvedConverter, MaxDescaleFactor);

        PdfImageRowDecodingParameters parameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(Descale(sampleSize.Width, descaleFactor), Descale(sampleSize.Height, descaleFactor)),
            Image.BitsPerComponent,
            resolvedConverter);

        List<PdfIntegerRectangle>? mappedRegions = MapRegionsToSampleGrid(regionsOfInterest, sampleSize, descaleFactor);

        _jpegRowDecoder = CreateJpegDecoder(encodedData, header, descaleFactor, ToJpegRegions(mappedRegions));

        return parameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
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

    public override void Cleanup() => _jpegRowDecoder = null;
}
