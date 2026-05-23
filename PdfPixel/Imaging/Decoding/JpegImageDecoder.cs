using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpg.Color;
using PdfPixel.Jpg.Decoding;
using PdfPixel.Jpg.Model;
using PdfPixel.Jpg.Readers;
using SkiaSharp;
using System;

namespace PdfPixel.Imaging.Decoding;

public sealed class JpegImageDecoder : PdfImageDecoder
{
    private IJpgDecoder _jpgRowDecoder;
    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public JpegImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
    {
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, SKRectI regionOfInterest, IPdfExecutionObserver observer)
    {
        if (!ValidateImageParameters())
            throw new InvalidOperationException($"JPEG image parameters are invalid (Name={Image.Name}).");


        ReadOnlyMemory<byte> encodedData;
        lock (contentLocker)
            encodedData = Image.GetImageData(observer);

        var header = JpgReader.ParseHeader(encodedData.Span);

        if (header == null || header.ContentOffset < 0)
            throw new InvalidOperationException($"JPEG header is invalid (Name={Image.Name}).");

        var resolvedConverter = Image.ColorSpaceConverter;
        if (resolvedConverter.IsDevice && JpgIccProfileReader.TryAssembleIccProfile(header, out var profileBytes))
        {
            resolvedConverter = new IccBasedConverter(header.ComponentCount, resolvedConverter, profileBytes);
        }

        int imageWidth = header.Width;
        int imageHeight = header.Height;
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new InvalidOperationException($"Invalid JPEG dimensions (Image={Image.Name}).");

        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        _jpgRowDecoder = CreateJpgDecoder(encodedData, header);
        _fullWidthRowBuffer = new byte[checked(header.ComponentCount * imageWidth)];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(IPdfExecutionObserver observer)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_jpgRowDecoder.TryReadRow(_fullWidthRowBuffer))
                throw new InvalidOperationException($"JPEG decode failed at image row {_currentImageRow} (Image={Image.Name}).");
            var tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, observer);
            _currentImageRow++;
            observer?.Notify();
            if (tiles != null) return tiles;
        }
        return null;
    }

    public override void Cleanup()
    {
        _jpgRowDecoder = null;
        _fullWidthRowBuffer = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }

    private IJpgDecoder CreateJpgDecoder(ReadOnlyMemory<byte> encodedData, JpgHeader header)
    {
        ReadOnlyMemory<byte> compressed = encodedData.Slice(header.ContentOffset);
        var colorTransform = Image.DecodeParms?.ColorTransform;

        var yuvMode = colorTransform switch
        {
            0 => JpgYuvMode.NoYuv,
            1 => JpgYuvMode.ForceYuv,
            _ => JpgYuvMode.Default
        };

        var colorParameters = new JpegColorConversionParameters
        {
            YuvMode = yuvMode,
            InvertCmykColors = false
        };

        return header.FrameType switch
        {
            JpgFrameType.ProgressiveDct => new JpgProgressiveDecoder(header, compressed, colorParameters),
            JpgFrameType.BaselineDct or JpgFrameType.ExtendedSequentialDct => new JpgBaselineDecoder(header, compressed, colorParameters),
            _ => throw new NotSupportedException($"JPEG frame type {header.FrameType} is not supported (Image={Image.Name}).")
        };
    }
}
