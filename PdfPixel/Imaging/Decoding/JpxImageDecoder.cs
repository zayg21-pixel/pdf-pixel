using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Jpx;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Decoding;

/// <summary>
/// Decodes JPEG 2000 images tile by tile, on the codestream's own tile grid: the color components through one tile
/// reader and, for /SMaskInData, the opacity component through another.
/// </summary>
internal class JpxImageDecoder : PdfImageDecoder
{
    private readonly JpxTileSource _colorTiles = new(plane: 0);
    private readonly JpxTileSource _alphaTiles = new(plane: 1);

    private ReadOnlyMemory<byte> _encodedData;
    private JpxHeader? _header;
    private PdfColorSpaceConverter? _resolvedConverter;
    private int _descaleFactor;
    private bool _hasReaders;

    public JpxImageDecoder(PdfImage image)
        : base(image)
    {
    }

    protected override PdfImageDecodeInfo SelectTiles(in PdfIntegerSize targetSize, PdfRectangle? region, IPdfExecutionObserver? observer)
    {
        EnsureReaders(targetSize, observer);

        if (_colorTiles.SampleInfo == null)
        {
            throw new InvalidOperationException($"JPX image has no color components (Reference={Image.Reference}).");
        }

        _colorTiles.SelectRegion(region, Cache, Image);
        _alphaTiles.SelectRegion(region, Cache, Image);

        return new PdfImageDecodeInfo(_colorTiles.SampleInfo, _alphaTiles.SampleInfo);
    }

    public override PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer) => _colorTiles.GetTile(tileIndex, observer);

    public override PdfImageTile? GetAlphaTile(int tileIndex, IPdfExecutionObserver? observer) => _alphaTiles.GetTile(tileIndex, observer);

    protected override void DropTiles()
    {
        _colorTiles.Clear();
        _alphaTiles.Clear();
    }

    /// <summary>
    /// Creates the tile readers for the reduction carrying at least <paramref name="targetSize"/> samples, unless they
    /// exist for it.
    /// </summary>
    private void EnsureReaders(in PdfIntegerSize targetSize, IPdfExecutionObserver? observer)
    {
        JpxHeader header = EnsureHeader(observer);
        PdfColorSpaceConverter resolvedConverter = EnsureConverter(header);
        PdfIntegerSize sourceSize = new((int)header.Width, (int)header.Height);
        int descaleFactor = (header.CodingStyle != null)
            ? SelectDescaleFactor(sourceSize, targetSize, resolvedConverter, 1 << header.CodingStyle.DecompositionLevels)
            : 1;

        if (_hasReaders && _descaleFactor == descaleFactor)
        {
            return;
        }

        ReadOnlyMemory<byte> codestream = _encodedData.Slice(header.CodestreamOffset);
        JpxTileReader colorReader = new(header, codestream, new JpxDecodingParameters(descaleFactor, GetColorComponents(header)));

        _colorTiles.SetReader(colorReader, resolvedConverter, descaleFactor);

        if (IncludesOpacityComponent() && header.OpacityComponentIndex >= 0)
        {
            // TODO: [MEDIUM] The second reader parses every tile's packet headers (tier 2) again and extracts the
            // tile parts of the whole codestream again; only tier 1 is split by component. One reader selecting the
            // color and opacity components, filling both caches on one decode of a tile, does both once.
            int[] opacityComponent = [header.OpacityComponentIndex];
            JpxTileReader alphaReader = new(header, codestream, new JpxDecodingParameters(descaleFactor, opacityComponent));
            PdfColorSpaceConverter? grayColorSpace = PdfColorSpaceResolver.ResolveDeviceConverter(1);

            if (grayColorSpace == null)
            {
                throw new InvalidOperationException("No device color space has one component.");
            }

            _alphaTiles.SetReader(alphaReader, grayColorSpace, descaleFactor);
        }

        _descaleFactor = descaleFactor;
        _hasReaders = true;
    }

    /// <summary>
    /// Whether the opacity channel of the codestream is the image's soft mask (ISO 32000-2 Table 87, SMaskInData).
    /// </summary>
    private bool IncludesOpacityComponent()
        => Image.MaskKind == PdfImageMaskKind.SoftMaskInData || Image.MaskKind == PdfImageMaskKind.PremultipliedSoftMaskInData;

    private JpxHeader EnsureHeader(IPdfExecutionObserver? observer)
    {
        if (_header != null)
        {
            return _header;
        }

        ReadOnlyMemory<byte> encodedData = Image.Stream.DecodeAsMemory(observer);
        var header = JpxHeader.Parse(encodedData.Span);

        _encodedData = encodedData;
        _header = header;

        return header;
    }

    private PdfColorSpaceConverter EnsureConverter(JpxHeader header)
    {
        if (_resolvedConverter != null)
        {
            return _resolvedConverter;
        }

        PdfColorSpaceConverter? resolvedConverter = ResolveConverter(header);
        if (resolvedConverter == null)
        {
            throw new InvalidOperationException($"Cannot determine color space for JPX image with {header.ComponentCount} components (Reference={Image.Reference}).");
        }

        _resolvedConverter = resolvedConverter;

        return resolvedConverter;
    }

    /// <summary>
    /// Returns every component of the codestream but the cdef opacity component, in codestream order.
    /// </summary>
    private static int[] GetColorComponents(JpxHeader header)
    {
        var components = new int[GetColorComponentCount(header)];
        int index = 0;

        for (int component = 0; component < header.ComponentCount; component++)
        {
            if (component != header.OpacityComponentIndex)
            {
                components[index++] = component;
            }
        }

        return components;
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

        PdfColorSpaceConverter? converter = Image.ColorSpace;
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

    /// <summary>
    /// The tiles one tile reader decodes into the image cache, the selection dropping those outside the selected region.
    /// </summary>
    private sealed class JpxTileSource
    {
        private readonly PdfImageTileSelection _tiles = new();
        private readonly int _plane;
        private JpxTileReader? _reader;
        private PdfIntegerRectangle? _regionTiles;

        public JpxTileSource(int plane) => _plane = plane;

        public PdfImageSampleInfo? SampleInfo { get; private set; }

        /// <summary>
        /// Reads tiles through <paramref name="reader"/>, decoding at <paramref name="descaleFactor"/>, from now on,
        /// dropping the tiles of the previous reader.
        /// </summary>
        public void SetReader(JpxTileReader reader, PdfColorSpaceConverter colorSpace, int descaleFactor)
        {
            PdfTileInfo grid = new(new PdfIntegerSize(reader.Width, reader.Height), new PdfIntegerSize(reader.TileWidth, reader.TileHeight));

            _reader = reader;
            SampleInfo = new PdfImageSampleInfo(grid, descaleFactor, reader.BitsPerComponent, reader.ComponentCount, colorSpace);
            _regionTiles = null;
        }

        public void SelectRegion(PdfRectangle? region, IPdfImageCache cache, PdfImage image)
        {
            if (SampleInfo == null)
            {
                return;
            }

            PdfIntegerRectangle regionTiles = GetRegionTileRange(SampleInfo, region);

            _tiles.SetSamples(SampleInfo, _plane);
            _tiles.SetCache(cache, image);

            if (_regionTiles != regionTiles)
            {
                _tiles.EvictOutside(regionTiles);
                _regionTiles = regionTiles;
            }
        }

        public PdfImageTile? GetTile(int tileIndex, IPdfExecutionObserver? observer)
        {
            if (_reader == null || SampleInfo == null)
            {
                return null;
            }

            if (!_tiles.Contains(tileIndex))
            {
                PdfIntegerRectangle bounds = SampleInfo.Grid.GetTilePosition(tileIndex);
                Span<byte> tile = _tiles.GetTileForWriting(tileIndex);

                try
                {
                    _reader.ReadTile(tileIndex, tile, PdfImageTileSelection.GetRowStride(SampleInfo, bounds.Width), 0, new JpxObserver(observer));
                }
                catch
                {
                    _tiles.Remove(tileIndex);
                    throw;
                }

                _tiles.Complete(tileIndex);
            }

            return _tiles.GetTile(tileIndex);
        }

        public void Clear()
        {
            _tiles.Clear();
            _regionTiles = null;
        }
    }

    private sealed class JpxObserver : IJpxExecutionObserver
    {
        private readonly IPdfExecutionObserver? _pdfObserver;

        public JpxObserver(IPdfExecutionObserver? pdfObserver) => _pdfObserver = pdfObserver;

        public void Notify() => _pdfObserver?.Notify();
    }
}
