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
using System.Threading;

namespace PdfPixel.Imaging.Decoding;

public sealed class JpegImageDecoder : PdfImageDecoder
{
    private readonly ReadOnlyMemory<byte> _encodedData;
    private readonly JpgHeader _jpgHeader;
    private readonly PdfColorSpaceConverter _resolvedConverter;

    private IJpgDecoder _jpgRowDecoder;
    private byte[] _fullWidthRowBuffer;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private int _currentImageRow;

    public JpegImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
    {
        _encodedData = image.GetImageData();
        if (_encodedData.IsEmpty) return;

        try { _jpgHeader = JpgReader.ParseHeader(_encodedData.Span); }
        catch (Exception ex) { Logger.LogError(ex, "JPEG header parse failed (Name={Name}).", image.Name); return; }

        if (_jpgHeader == null || _jpgHeader.ContentOffset < 0) return;

        _resolvedConverter = image.ColorSpaceConverter;
        if (_resolvedConverter.IsDevice && JpgIccProfileReader.TryAssembleIccProfile(_jpgHeader, out var profileBytes))
            _resolvedConverter = new IccBasedConverter(_jpgHeader.ComponentCount, _resolvedConverter, profileBytes);
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, SKMatrix ctm, SKRectI regionOfInterest)
    {
        if (!ValidateImageParameters())
            throw new InvalidOperationException($"JPEG image parameters are invalid (Name={Image.Name}).");
        if (_encodedData.IsEmpty)
            throw new InvalidOperationException($"JPEG image data is empty (Name={Image.Name}).");
        if (_jpgHeader == null || _jpgHeader.ContentOffset < 0)
            throw new InvalidOperationException($"JPEG header is invalid (Name={Image.Name}).");

        int imageWidth = _jpgHeader.Width;
        int imageHeight = _jpgHeader.Height;
        if (imageWidth <= 0 || imageHeight <= 0)
            throw new InvalidOperationException($"Invalid JPEG dimensions (Image={Image.Name}).");

        _imageParameters = PdfImageRowDecodingParameters.FromImage(Image, context, ctm);

        _jpgRowDecoder = CreateJpgDecoder();
        _fullWidthRowBuffer = new byte[checked(_jpgHeader.ComponentCount * imageWidth)];
        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, regionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(CancellationToken cancellationToken = default)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_jpgRowDecoder.TryReadRow(_fullWidthRowBuffer))
                throw new InvalidOperationException($"JPEG decode failed at image row {_currentImageRow} (Image={Image.Name}).");
            var tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, cancellationToken);
            _currentImageRow++;
            cancellationToken.ThrowIfCancellationRequested();
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

    private IJpgDecoder CreateJpgDecoder()
    {
        ReadOnlyMemory<byte> compressed = _encodedData.Slice(_jpgHeader.ContentOffset);
        return _jpgHeader.FrameType switch
        {
            JpgFrameType.ProgressiveDct => new JpgProgressiveDecoder(_jpgHeader, compressed),
            JpgFrameType.BaselineDct or JpgFrameType.ExtendedSequentialDct => new JpgBaselineDecoder(_jpgHeader, compressed),
            _ => throw new NotSupportedException($"JPEG frame type {_jpgHeader.FrameType} is not supported (Image={Image.Name}).")
        };
    }
}
