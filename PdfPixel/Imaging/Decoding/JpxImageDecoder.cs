using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

internal class JpxImageDecoder : PdfImageDecoder
{
    private PdfColorSpaceConverter? _resolvedConverter;
    private JpxTileToRowConverter? _rowConverter;

    public JpxImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
    }

    public override PdfImageRowDecodingParameters Initialize(
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        in PdfMatrix ctm,
        IPdfExecutionObserver? observer)
    {
        ReadOnlyMemory<byte> encodedData = Image.Stream.DecodeAsMemory(observer);

        JpxHeader jpxHeader = JpxReader.ParseHeader(encodedData.Span);

        _resolvedConverter = ResolveConverter(jpxHeader);
        if (_resolvedConverter == null)
        {
            throw new InvalidOperationException($"Cannot determine color space for JPX image with {jpxHeader.ComponentCount} components (Reference={Image.Reference}).");
        }

        bool includeOpacityComponent = Context.ApplyMask && Image.SoftMaskInData != PdfImageSoftMaskInData.None;
        JpxDecodingParameters jpxDecodingParameters = ComputeDecodingParameters(jpxHeader, ctm, regionsOfInterest, _resolvedConverter, includeOpacityComponent);
        JpxTileProvider tileProvider = new(
            jpxHeader,
            encodedData.Span.Slice(jpxHeader.CodestreamOffset),
            jpxDecodingParameters);

        _rowConverter = new JpxTileToRowConverter(jpxHeader, tileProvider, jpxDecodingParameters);

        bool hasInterleavedAlpha = _rowConverter.HasAlphaChannel;
        float[]? matte = hasInterleavedAlpha ? null : ResolveSoftMaskMatte(_resolvedConverter);
        PdfImageAlphaType alphaType = hasInterleavedAlpha
            ? PdfImageAlphaType.Unpremultiplied
            : ResolveMaskAlphaType(matte);

        PdfIntegerSize decodedSize = new(_rowConverter.Width, _rowConverter.Height);
        PdfColorConversionParameters colorConversionParameters = new(Image.RenderingIntent, Context.TransferFunction, Context.UseBlackPointCompensation, Context.Host.Resources.DeviceColorMapping);

        PdfImageRowDecodingParameters parameters = new(
            Context,
            decodedSize.Width,
            decodedSize.Height,
            _rowConverter.BitsPerComponent,
            colorConversionParameters,
            _resolvedConverter,
            Image.HasImageMask,
            AppliedColorKeyMask,
            Image.Decode,
            PdfImageCommandUtilities.GetScaledSize(ctm, decodedSize),
            alphaType,
            hasInterleavedAlpha,
            matte);

        return parameters;
    }

    public override bool TryReadNextRow(in Span<byte> destination, IPdfExecutionObserver? observer)
    {
        if (_rowConverter == null)
        {
            return false;
        }

        JpxObserver jpxObserver = new(observer);
        if (!_rowConverter.TryGetNextRow(destination, jpxObserver))
        {
            throw new InvalidOperationException($"JPX decode failed (Reference={Image.Reference}).");
        }

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

        return PdfColorSpaceResolver.ResolveDeviceConverter(colorComponents);
    }

    private JpxDecodingParameters ComputeDecodingParameters(
        JpxHeader header,
        in PdfMatrix ctm,
        IReadOnlyList<PdfIntegerRectangle>? regionsOfInterest,
        PdfColorSpaceConverter resolvedConverter,
        bool includeOpacityComponent)
    {
        PdfIntegerSize sourceSize = new((int)header.Width, (int)header.Height);

        int descaleFactor = (header.CodingStyle != null)
            ? ComputeDescaleFactor(sourceSize, ctm, resolvedConverter, 1 << header.CodingStyle.DecompositionLevels)
            : 1;

        List<PdfIntegerRectangle>? mappedRegions = MapRegionsToSampleGrid(regionsOfInterest, sourceSize, descaleFactor);

        return new JpxDecodingParameters(descaleFactor, ToJpxRegions(mappedRegions), includeOpacityComponent);
    }

    private static List<JpxRectangle>? ToJpxRegions(List<PdfIntegerRectangle>? regionsOfInterest)
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
    }
}
