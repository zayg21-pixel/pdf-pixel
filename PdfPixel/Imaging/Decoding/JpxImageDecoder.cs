using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using SkiaSharp;
using System;

namespace PdfPixel.Imaging.Decoding;

internal class JpxImageDecoder : PdfImageDecoder
{
    private PdfColorSpaceConverter? _resolvedConverter;
    private JpxTileToRowConverter? _rowConverter;
    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private byte[]? _fullWidthRowBuffer;
    private int _currentImageRow;

    public JpxImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
    }

    public override void Initialize(PdfTileInfo tileInfo, ImageDecodingContext context, object contentLocker, SKMatrix ctm, SKRectI regionOfInterest, IPdfExecutionObserver? observer)
    {
        ReadOnlyMemory<byte> encodedData;
        lock (contentLocker)
        {
            encodedData = Image.GetImageData(observer);
        }

        JpxHeader jpxHeader = JpxReader.ParseHeader(encodedData.Span);

        _resolvedConverter = ResolveConverter(jpxHeader);
        if (_resolvedConverter == null)
        {
            throw new InvalidOperationException($"Cannot determine color space for JPX image with {jpxHeader.ComponentCount} components (Name={Image.Name}).");
        }

        JpxDecodingParameters jpxDecodingParameters = ComputeDecodingParameters(jpxHeader, ctm);
        JpxTileProvider tileProvider = new(
            jpxHeader,
            encodedData.Span.Slice(jpxHeader.CodestreamOffset),
            jpxDecodingParameters);

        _rowConverter = new JpxTileToRowConverter(jpxHeader, tileProvider, jpxDecodingParameters);

        SKSizeI? downscaledSize = PdfImageRowDecodingParameters.ComputeDownscaledSize(_rowConverter.Width, _rowConverter.Height, _resolvedConverter, context, ctm);
        _imageParameters = new PdfImageRowDecodingParameters(
            context,
            _rowConverter.Width,
            _rowConverter.Height,
            _rowConverter.BitsPerComponent,
            Image.RenderingIntent,
            _resolvedConverter,
            Image.HasImageMask,
            Image.MaskArray,
            Image.DecodeArray,
            downscaledSize: downscaledSize,
            descaleFactor: jpxDecodingParameters.DescaleFactor);

        _fullWidthRowBuffer = new byte[((_rowConverter.Width * _rowConverter.ComponentCount * _rowConverter.BitsPerComponent) + 7) / 8];

        // TODO: [HIGH] support transparency

        int descale = jpxDecodingParameters.DescaleFactor;
        SKRectI scaledRegionOfInterest = SKRectI.Create(
            regionOfInterest.Left / descale,
            regionOfInterest.Top / descale,
            jpxDecodingParameters.ReduceDimension(regionOfInterest.Width),
            jpxDecodingParameters.ReduceDimension(regionOfInterest.Height));

        SKSizeI descaledTileSize = new(
            jpxDecodingParameters.ReduceDimension(tileInfo.TileWidth),
            jpxDecodingParameters.ReduceDimension(tileInfo.TileHeight));
        _tilingContext = new PdfImageTilingContext(descaledTileSize, tileInfo, _imageParameters, scaledRegionOfInterest, LoggerFactory);
        _currentImageRow = 0;
    }

    public override PdfImageTile[]? DecodeNextTiles(IPdfExecutionObserver? observer)
    {
        if (_imageParameters == null || _rowConverter == null || _tilingContext == null)
        {
            return null;
        }

        JpxObserver jpxObserver = new(observer);
        while (_currentImageRow < _imageParameters.Height)
        {
            if (!_rowConverter.TryGetNextRow(_fullWidthRowBuffer, jpxObserver))
            {
                throw new InvalidOperationException($"JPX decode failed at row {_currentImageRow} (Image={Image.Name}).");
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

    private PdfColorSpaceConverter? ResolveConverter(JpxHeader header)
    {
        PdfColorSpaceConverter? converter = Image.ColorSpaceConverter;
        if (converter != null && (converter is IndexedConverter || converter.Components == header.ComponentCount))
        {
            return converter;
        }

        if (converter != null)
        {
            return converter;
        }

        return Image.Page.Cache.ColorSpace.ResolveDeviceConverter(header.ComponentCount);
    }

    private static JpxDecodingParameters ComputeDecodingParameters(JpxHeader header, SKMatrix ctm)
    {
        SKSizeI sourceSize = new((int)header.Width, (int)header.Height);
        SKSizeI? targetSize = PdfImageCommandUtilities.GetScaledSize(ctm, sourceSize);

        if (!targetSize.HasValue || header.CodingStyle == null)
        {
            return JpxDecodingParameters.Default;
        }

        int maxLevels = header.CodingStyle.DecompositionLevels;
        int descaleFactor = 1;

        for (int candidate = 2; candidate <= (1 << maxLevels); candidate *= 2)
        {
            int reducedWidth = Math.Max(1, (sourceSize.Width + candidate - 1) / candidate);
            int reducedHeight = Math.Max(1, (sourceSize.Height + candidate - 1) / candidate);

            if (reducedWidth >= targetSize.Value.Width && reducedHeight >= targetSize.Value.Height)
            {
                descaleFactor = candidate;
            }
            else
            {
                break;
            }
        }

        return new JpxDecodingParameters(descaleFactor);
    }

    private sealed class JpxObserver : IJpxExectionObserver // TODO: [HIGH] add default implementations of ExectionObserver to JBIG2 and JPX
    {
        private readonly IPdfExecutionObserver? _pdfObserver;

        public JpxObserver(IPdfExecutionObserver? pdfObserver) => _pdfObserver = pdfObserver;

        public void Notify() => _pdfObserver?.Notify();
    }

    public override void Cleanup()
    {
        _rowConverter = null;
        _resolvedConverter = null;
        _tilingContext?.Dispose();
        _tilingContext = null;
        _imageParameters = null;
        _fullWidthRowBuffer = null;
        _currentImageRow = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tilingContext?.Dispose();
        }
    }
}
