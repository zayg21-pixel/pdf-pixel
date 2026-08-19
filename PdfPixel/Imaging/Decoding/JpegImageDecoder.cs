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

        PdfImageRowDecodingParameters parameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(header.Width, decodedHeight),
            Image.BitsPerComponent,
            resolvedConverter);

        _jpgRowDecoder = CreateJpgDecoder(encodedData, header);

        return parameters;
    }

    public override bool TryReadNextRow(byte[] destination, IPdfExecutionObserver? observer)
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

    private IJpgDecoder CreateJpgDecoder(in ReadOnlyMemory<byte> encodedData, JpgHeader header)
    {
        ReadOnlyMemory<byte> compressed = encodedData.Slice(header.ContentOffset);
        int? colorTransform = Image.DecodeParms?.ColorTransform;

        JpgYuvMode yuvMode = colorTransform switch
        {
            0 => JpgYuvMode.NoYuv,
            1 => JpgYuvMode.ForceYuv,
            _ => JpgYuvMode.Default
        };

        JpegColorConversionParameters colorParameters = new()
        {
            YuvMode = yuvMode,
            InvertCmykColors = false
        };

        return header.FrameType switch
        {
            JpgFrameType.ProgressiveDct => new JpgProgressiveDecoder(header, compressed, colorParameters),
            JpgFrameType.BaselineDct or JpgFrameType.ExtendedSequentialDct => new JpgBaselineDecoder(header, compressed, colorParameters),
            _ => throw new NotSupportedException($"JPEG frame type {header.FrameType} is not supported (SourceReference={Image.SourceReference}).")
        };
    }

    public override void Cleanup() => _jpgRowDecoder = null;
}
