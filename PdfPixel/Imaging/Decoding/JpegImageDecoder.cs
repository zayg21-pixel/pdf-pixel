using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpg.Color;
using PdfPixel.Jpg.Decoding;
using PdfPixel.Jpg.Model;
using PdfPixel.Jpg.Readers;
using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal sealed class JpegImageDecoder : PdfImageDecoder
{
    private const int MaxDescaleFactor = 8;

    private IJpgDecoder? _jpgRowDecoder;

    public JpegImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        object contentLocker,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        if (!ValidateImageParameters())
        {
            throw new InvalidOperationException($"JPEG image parameters are invalid (SourceReference={Image.SourceReference}).");
        }

        ReadOnlyMemory<byte> encodedData;
        lock (contentLocker)
        {
            encodedData = Image.GetImageData(observer);
        }

        JpgHeader header = JpgReader.ParseHeader(encodedData.Span);

        if (header == null || header.ContentOffset < 0)
        {
            throw new InvalidOperationException($"JPEG header is invalid (SourceReference={Image.SourceReference}).");
        }

        PdfColorSpaceConverter resolvedConverter = ResolvedColorSpaceConverter;
        if ((Context.ColorSpaceConverter == null || resolvedConverter.IsDevice) && JpgIccProfileReader.TryAssembleIccProfile(header, out byte[]? profileBytes))
        {
            resolvedConverter = new PdfIccColorSpaceConverter(header.ComponentCount, resolvedConverter, profileBytes);
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

        _jpgRowDecoder = CreateJpgDecoder(encodedData, header, descaleFactor, ToJpgRegions(mappedRegions));

        return parameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_jpgRowDecoder == null)
        {
            return false;
        }

        if (!_jpgRowDecoder.TryReadRow(destination))
        {
            throw new InvalidOperationException($"JPEG decode failed (SourceReference={Image.SourceReference}).");
        }

        return true;
    }

    private static List<JpgRectangle>? ToJpgRegions(List<PdfIntegerRectangle>? regionsOfInterest)
    {
        if (regionsOfInterest == null)
        {
            return null;
        }

        List<JpgRectangle> jpgRegionsOfInterest = new(regionsOfInterest.Count);
        for (int index = 0; index < regionsOfInterest.Count; index++)
        {
            PdfIntegerRectangle region = regionsOfInterest[index];
            jpgRegionsOfInterest.Add(new JpgRectangle(region.Left, region.Top, region.Width, region.Height));
        }

        return jpgRegionsOfInterest;
    }

    private IJpgDecoder CreateJpgDecoder(
        in ReadOnlyMemory<byte> encodedData,
        JpgHeader header,
        int descaleFactor,
        IReadOnlyList<JpgRectangle>? regionsOfInterest)
    {
        int? colorTransform = Image.DecodeParms?.ColorTransform;

        JpgYuvMode yuvMode = colorTransform switch
        {
            0 => JpgYuvMode.NoYuv,
            1 => JpgYuvMode.ForceYuv,
            _ => JpgYuvMode.Default
        };

        JpgDecoderOptions decoderOptions = new()
        {
            YuvMode = yuvMode,
            InvertCmykColors = false,
            DescaleFactor = descaleFactor,
            RegionsOfInterest = regionsOfInterest
        };

        return JpgDecoderFactory.Create(header, encodedData, decoderOptions);
    }

    public override void Cleanup() => _jpgRowDecoder = null;
}
