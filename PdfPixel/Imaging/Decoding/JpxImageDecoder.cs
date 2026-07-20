using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal class JpxImageDecoder : PdfImageDecoder
{
    private PdfColorSpaceConverter? _resolvedConverter;
    private JpxTileToRowConverter? _rowConverter;
    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private byte[]? _fullWidthRowBuffer;
    private int _currentImageRow;

    private readonly PdfColorSpaceConverter? _deviceGray;
    private readonly PdfColorSpaceConverter? _deviceRgb;
    private readonly PdfColorSpaceConverter? _deviceCmyk;

    public JpxImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        ColorSpaceResolver colorSpace = context.Page.Cache.ColorSpace;
        _deviceGray = colorSpace.ResolveDeviceConverter(1);
        _deviceRgb = colorSpace.ResolveDeviceConverter(3);
        _deviceCmyk = colorSpace.ResolveDeviceConverter(4);
    }

    public override void Initialize(PdfTileInfo tileInfo, object contentLocker, in PdfMatrix ctm, HashSet<int>? tileIndexesToDecode, IPdfExecutionObserver? observer)
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
            throw new InvalidOperationException($"Cannot determine color space for JPX image with {jpxHeader.ComponentCount} components (SourceReference={Image.SourceReference}).");
        }

        JpxDecodingParameters jpxDecodingParameters = ComputeDecodingParameters(jpxHeader, ctm, tileInfo, tileIndexesToDecode, _resolvedConverter);
        JpxTileProvider tileProvider = new(
            jpxHeader,
            encodedData.Span.Slice(jpxHeader.CodestreamOffset),
            jpxDecodingParameters);

        _rowConverter = new JpxTileToRowConverter(jpxHeader, tileProvider, jpxDecodingParameters);

        PdfIntegerSize? downscaledSize = PdfImageCommandUtilities.GetScaledSize(ctm, new PdfIntegerSize(_rowConverter.Width, _rowConverter.Height));

        _imageParameters = new PdfImageRowDecodingParameters(
            Context,
            _rowConverter.Width,
            _rowConverter.Height,
            _rowConverter.BitsPerComponent,
            Image.RenderingIntent,
            _resolvedConverter,
            Image.HasImageMask,
            Image.MaskArray,
            Image.Decode,
            downscaledSize: downscaledSize,
            hasAlphaChannel: _rowConverter.HasAlphaChannel);

        _fullWidthRowBuffer = new byte[((_rowConverter.Width * _rowConverter.ComponentCount * _rowConverter.BitsPerComponent) + 7) / 8];

        _tilingContext = new PdfImageTilingContext(tileInfo, _imageParameters, tileIndexesToDecode, LoggerFactory);
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
                throw new InvalidOperationException($"JPX decode failed at row {_currentImageRow} (SourceReference={Image.SourceReference}).");
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

    private static int GetColorComponentCount(JpxHeader header)
    {
        if (header.OpacityComponentIndex >= 0)
        {
            return header.ComponentCount - 1;
        }

        return header.ComponentCount;
    }

    private PdfColorSpaceConverter? ResolveConverter(JpxHeader header)
    {
        int colorComponents = GetColorComponentCount(header);

        PdfColorSpaceConverter? converter = Context.ColorSpaceConverter;
        if (converter != null && (converter is IndexedConverter || converter.Components == colorComponents))
        {
            return converter;
        }

        if (converter != null)
        {
            return converter;
        }

        return colorComponents switch
        {
            1 => _deviceGray,
            3 => _deviceRgb,
            4 => _deviceCmyk,
            _ => null
        };
    }

    private static JpxDecodingParameters ComputeDecodingParameters(JpxHeader header, in PdfMatrix ctm, PdfTileInfo tileInfo, HashSet<int>? tileIndexesToDecode, PdfColorSpaceConverter resolvedConverter)
    {
        IReadOnlyList<JpxRegion>? regionsOfInterest = ComputeRegionsOfInterest(tileInfo, tileIndexesToDecode);

        // Indexed samples are palette indices; never reconstruct them at a reduced DWT level.
        if (resolvedConverter is IndexedConverter)
        {
            return new JpxDecodingParameters(1, regionsOfInterest);
        }

        PdfIntegerSize sourceSize = new((int)header.Width, (int)header.Height);
        PdfIntegerSize? targetSize = PdfImageCommandUtilities.GetScaledSize(ctm, sourceSize);

        if (!targetSize.HasValue || header.CodingStyle == null)
        {
            return new JpxDecodingParameters(1, regionsOfInterest);
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

        return new JpxDecodingParameters(descaleFactor, regionsOfInterest);
    }

    private static List<JpxRegion>? ComputeRegionsOfInterest(PdfTileInfo tileInfo, HashSet<int>? tileIndexesToDecode)
    {
        if (tileIndexesToDecode == null)
        {
            return null;
        }

        List<JpxRegion> regionsOfInterest = new(tileIndexesToDecode.Count);
        foreach (int tileIndex in tileIndexesToDecode)
        {
            PdfIntegerRectangle tilePosition = tileInfo.GetTilePosition(tileIndex);
            regionsOfInterest.Add(new JpxRegion(tilePosition.Left, tilePosition.Top, tilePosition.Width, tilePosition.Height));
        }

        return regionsOfInterest;
    }

    private sealed class JpxObserver : IJpxExectionObserver
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
