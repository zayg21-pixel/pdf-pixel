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
    private byte[]? _fullWidthRowBuffer;

    private readonly PdfColorSpaceConverter? _deviceGray;
    private readonly PdfColorSpaceConverter? _deviceRgb;
    private readonly PdfColorSpaceConverter? _deviceCmyk;

    public JpxImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
        PdfColorSpaceResolver colorSpace = context.Page.Cache.ColorSpace;
        _deviceGray = colorSpace.ResolveDeviceConverter(1);
        _deviceRgb = colorSpace.ResolveDeviceConverter(3);
        _deviceCmyk = colorSpace.ResolveDeviceConverter(4);
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        object contentLocker,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
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

        // TODO: [MEDIUM] Un-blend the colour for /SMaskInData 2, which is read here but treated as
        // mode 1. Mode 2 says the colour channels are premultiplied, which is /Matte with a black
        // backdrop — c' = m + a(c - m) at m = 0 — so it wants the same un-blend as the MatteArray
        // that SoftMaskImageExecutionContext already carries, and the colour space needs no
        // transform. The two do not unify at the same point though: mode 2 has its alpha in the
        // row, while /Matte gets it from a separately tiled image and can only be undone once both
        // tiles meet. The corpus sets the entry in issue11306, issue16782 and isssue18194.
        bool includeOpacityComponent = Image.SoftMaskInData != PdfImageSoftMaskInData.None;
        JpxDecodingParameters jpxDecodingParameters = ComputeDecodingParameters(jpxHeader, ctm, regionsOfInterest, _resolvedConverter, includeOpacityComponent);
        JpxTileProvider tileProvider = new(
            jpxHeader,
            encodedData.Span.Slice(jpxHeader.CodestreamOffset),
            jpxDecodingParameters);

        _rowConverter = new JpxTileToRowConverter(jpxHeader, tileProvider, jpxDecodingParameters);

        PdfImageRowDecodingParameters parameters = CreateRowDecodingParameters(
            ctm,
            new PdfIntegerSize(_rowConverter.Width, _rowConverter.Height),
            _rowConverter.BitsPerComponent,
            _resolvedConverter,
            hasAlphaChannel: _rowConverter.HasAlphaChannel);

        _fullWidthRowBuffer = new byte[((_rowConverter.Width * _rowConverter.ComponentCount * _rowConverter.BitsPerComponent) + 7) / 8];

        return parameters;
    }

    public override bool TryReadNextRow(IPdfExecutionObserver? observer, out ReadOnlySpan<byte> row)
    {
        if (_rowConverter == null || _fullWidthRowBuffer == null)
        {
            row = default;
            return false;
        }

        JpxObserver jpxObserver = new(observer);
        if (!_rowConverter.TryGetNextRow(_fullWidthRowBuffer, jpxObserver))
        {
            throw new InvalidOperationException($"JPX decode failed (SourceReference={Image.SourceReference}).");
        }

        row = _fullWidthRowBuffer;
        return true;
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
        if (converter != null && (converter is PdfIndexedColorSpaceConverter || converter.Components == colorComponents))
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

    private static JpxDecodingParameters ComputeDecodingParameters(
        JpxHeader header,
        in PdfMatrix ctm,
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        PdfColorSpaceConverter resolvedConverter,
        bool includeOpacityComponent)
    {
        IReadOnlyList<JpxRectangle>? jpxRegionsOfInterest = ToJpxRegions(regionsOfInterest);

        // Indexed samples are palette indices; never reconstruct them at a reduced DWT level.
        if (resolvedConverter is PdfIndexedColorSpaceConverter)
        {
            return new JpxDecodingParameters(1, jpxRegionsOfInterest, includeOpacityComponent);
        }

        PdfIntegerSize sourceSize = new((int)header.Width, (int)header.Height);
        PdfIntegerSize? targetSize = PdfImageCommandUtilities.GetScaledSize(ctm, sourceSize);

        if (!targetSize.HasValue || header.CodingStyle == null)
        {
            return new JpxDecodingParameters(1, jpxRegionsOfInterest, includeOpacityComponent);
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

        return new JpxDecodingParameters(descaleFactor, jpxRegionsOfInterest, includeOpacityComponent);
    }

    private static List<JpxRectangle>? ToJpxRegions(IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest)
    {
        if (regionsOfInterest == null)
        {
            return null;
        }

        List<JpxRectangle> jpxRegionsOfInterest = new(regionsOfInterest.Count);
        for (int index = 0; index < regionsOfInterest.Count; index++)
        {
            PdfIntegerRectangle region = regionsOfInterest[index];
            jpxRegionsOfInterest.Add(new JpxRectangle(region.Left, region.Top, region.Width, region.Height));
        }

        return jpxRegionsOfInterest;
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
        _fullWidthRowBuffer = null;
    }
}
