using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Jpx.Decoding;
using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.Imaging.Decoding;

public class JpxImageDecoder : PdfImageDecoder
{
    private readonly ReadOnlyMemory<byte> _encodedData;
    private readonly JpxHeader _jpxHeader;

    private JpxTileProvider _tileProvider;
    private IJpxTileDecoder _tileDecoder;
    private JpxDecodingParameters _jpxDecodingParameters;
    private PdfColorSpaceConverter _resolvedConverter;
    private JpxTileToRowConverter _rowConverter;
    private PdfImageTilingContext _tilingContext;
    private PdfImageRowDecodingParameters _imageParameters;
    private byte[] _fullWidthRowBuffer;
    private int _currentImageRow;

    public JpxImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
        _encodedData = image.GetImageData();
        if (!_encodedData.IsEmpty)
            _jpxHeader = JpxReader.ParseHeader(_encodedData.Span);
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, SKMatrix ctm, SKRectI regionOfInterest)
    {
        if (_encodedData.IsEmpty || _jpxHeader == null
            || _jpxHeader.Width == 0 || _jpxHeader.Height == 0 || _jpxHeader.ComponentCount == 0)
            throw new InvalidOperationException($"JPX data or header is invalid (Name={Image.Name}).");

        _resolvedConverter = ResolveConverter(_jpxHeader);
        if (_resolvedConverter == null)
            throw new InvalidOperationException($"Cannot determine color space for JPX image with {_jpxHeader.ComponentCount} components (Name={Image.Name}).");

        _jpxDecodingParameters = ComputeDecodingParameters(_jpxHeader, ctm);
        _tileDecoder = JpxTileDecoderFactory.CreateDecoder(_jpxHeader);
        _tileProvider = new JpxTileProvider(
            _jpxHeader,
            _encodedData.Span.Slice(_jpxHeader.CodestreamOffset),
            _tileDecoder,
            _jpxDecodingParameters);

        _rowConverter = new JpxTileToRowConverter(_jpxHeader, _tileProvider, _jpxDecodingParameters);

        var downscaledSize = PdfImageRowDecodingParameters.ComputeDownscaledSize(_rowConverter.Width, _rowConverter.Height, _resolvedConverter, context, ctm);
        _imageParameters = new PdfImageRowDecodingParameters(
            context, _rowConverter.Width, _rowConverter.Height, _rowConverter.BitsPerComponent,
            Image.RenderingIntent, _resolvedConverter, Image.HasImageMask, Image.MaskArray,
            Image.DecodeArray, downscaledSize: downscaledSize, descaleFactor: _jpxDecodingParameters.DescaleFactor);

        _fullWidthRowBuffer = new byte[(_rowConverter.Width * _rowConverter.ComponentCount * _rowConverter.BitsPerComponent + 7) / 8];

        int descale = _jpxDecodingParameters.DescaleFactor;
        SKRectI scaledRegionOfInterest = SKRectI.Create(
            regionOfInterest.Left / descale,
            regionOfInterest.Top / descale,
            _jpxDecodingParameters.ReduceDimension(regionOfInterest.Width),
            _jpxDecodingParameters.ReduceDimension(regionOfInterest.Height));

        _tilingContext = new PdfImageTilingContext(new SKSizeI(tileInfo.TileWidth, tileInfo.TileHeight), _imageParameters, ctm, scaledRegionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[] DecodeNextTiles(CancellationToken cancellationToken = default)
    {
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_rowConverter.TryGetNextRow(_fullWidthRowBuffer, cancellationToken))
                throw new InvalidOperationException($"JPX decode failed at row {_currentImageRow} (Image={Image.Name}).");
            var tiles = _tilingContext.WriteRowAndTryGetTiles(_currentImageRow, _fullWidthRowBuffer, cancellationToken);
            _currentImageRow++;
            cancellationToken.ThrowIfCancellationRequested();
            if (tiles != null) return tiles;
        }
        return null;
    }

    public override void Cleanup()
    {
        _rowConverter?.Dispose();
        _rowConverter = null;
        _tileProvider = default;
        _tileDecoder = null;
        _resolvedConverter = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _fullWidthRowBuffer = null;
        _currentImageRow = 0;
    }

    private PdfColorSpaceConverter ResolveConverter(JpxHeader header)
    {
        var converter = Image.ColorSpaceConverter;
        if (converter != null && (converter is IndexedConverter || converter.Components == header.ComponentCount))
            return converter;

        return header.ComponentCount switch
        {
            1 => DeviceGrayConverter.Instance,
            3 => DeviceRgbConverter.Instance,
            4 => DeviceCmykConverter.Instance,
            _ => null
        };
    }

    private static JpxDecodingParameters ComputeDecodingParameters(JpxHeader header, SKMatrix ctm)
    {
        var sourceSize = new SKSizeI((int)header.Width, (int)header.Height);
        SKSizeI? targetSize = PdfImageCommandUtilities.GetScaledSize(ctm, sourceSize);

        if (!targetSize.HasValue || header.CodingStyle == null)
            return JpxDecodingParameters.Default;

        int maxLevels = header.CodingStyle.DecompositionLevels;
        int descaleFactor = 1;

        for (int candidate = 2; candidate <= (1 << maxLevels); candidate *= 2)
        {
            int reducedWidth = Math.Max(1, (sourceSize.Width + candidate - 1) / candidate);
            int reducedHeight = Math.Max(1, (sourceSize.Height + candidate - 1) / candidate);

            if (reducedWidth >= targetSize.Value.Width && reducedHeight >= targetSize.Value.Height)
                descaleFactor = candidate;
            else
                break;
        }

        return new JpxDecodingParameters(descaleFactor);
    }
}
