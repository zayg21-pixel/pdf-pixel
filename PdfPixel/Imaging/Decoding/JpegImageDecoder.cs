using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpg.Color;
using PdfPixel.Jpg.Decoding;
using PdfPixel.Jpg.Model;
using PdfPixel.Jpg.Readers;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal sealed class JpegImageDecoder : PdfImageDecoder
{
    private IJpgDecoder? _jpgRowDecoder;
    private byte[]? _fullWidthRowBuffer;
    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private int _currentImageRow;

    public JpegImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
    }

    public override void Initialize(PdfTileInfo tileInfo, object contentLocker, in PdfMatrix ctm, HashSet<int>? tileIndexesToDecode, IPdfExecutionObserver observer)
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

        PdfIntegerSize? downscaledSize = PdfImageCommandUtilities.GetScaledSize(ctm, new PdfIntegerSize(header.Width, decodedHeight));

        _imageParameters = new PdfImageRowDecodingParameters(
            Context,
            header.Width,
            decodedHeight,
            Image.BitsPerComponent,
            Image.RenderingIntent,
            resolvedConverter,
            Image.HasImageMask,
            Image.MaskArray,
            Image.Decode,
            downscaledSize);

        _jpgRowDecoder = CreateJpgDecoder(encodedData, header);
        _fullWidthRowBuffer = new byte[checked(header.ComponentCount * header.Width)];
        _tilingContext = new PdfImageTilingContext(tileInfo, _imageParameters, tileIndexesToDecode, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[]? DecodeNextTiles(IPdfExecutionObserver? observer)
    {
        if (_imageParameters == null || _jpgRowDecoder == null || _fullWidthRowBuffer == null || _tilingContext == null)
        {
            return null;
        }

        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_jpgRowDecoder.TryReadRow(_fullWidthRowBuffer))
            {
                throw new InvalidOperationException($"JPEG decode failed at image row {_currentImageRow} (SourceReference={Image.SourceReference}).");
            }

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

    public override void Cleanup()
    {
        _jpgRowDecoder = null;
        _fullWidthRowBuffer = null;
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }
}
