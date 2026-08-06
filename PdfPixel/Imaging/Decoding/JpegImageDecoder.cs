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

/// <summary>
/// Decodes JPEG-compressed PDF images row by row, resolving color space and producing
/// tiled <see cref="PdfImageTile"/> results ready for rendering.
/// </summary>
public sealed class JpegImageDecoder : PdfImageDecoder
{
    private IJpgDecoder? _jpgRowDecoder;
    private byte[]? _fullWidthRowBuffer;
    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _imageParameters;
    private int _currentImageRow;

    /// <summary>
    /// Initializes a new <see cref="JpegImageDecoder"/> for the given PDF image.
    /// </summary>
    /// <param name="image">The source PDF image descriptor.</param>
    /// <param name="context">Decoding context holding the page and color space resolved for <paramref name="image"/>.</param>
    /// <param name="loggerFactory">Logger factory used to create per-decoder loggers.</param>
    public JpegImageDecoder(PdfImage image, ImageDecodingContext context, ILoggerFactory loggerFactory)
        : base(image, context, loggerFactory)
    {
    }

    /// <summary>
    /// Parses the JPEG stream header, resolves the color space converter (including embedded ICC
    /// profiles), computes the downscaled output size, and prepares the row decoder and tiling context.
    /// </summary>
    /// <param name="tileInfo">Tile grid dimensions for this decode pass.</param>
    /// <param name="contentLocker">Lock object used to serialize access to the compressed image data.</param>
    /// <param name="ctm">Current transformation matrix, used to compute the scaled output size.</param>
    /// <param name="tileIndexesToDecode">Indexes of tiles that must be decoded; every other tile is produced as a skipped placeholder. Null means every tile must be decoded.</param>
    /// <param name="observer">Observer notified on each decoded row.</param>
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
            resolvedConverter = new IccBasedConverter(header.ComponentCount, resolvedConverter, profileBytes);
        }

        PdfIntegerSize? downscaledSize = PdfImageCommandUtilities.GetScaledSize(ctm, new PdfIntegerSize(header.Width, header.Height));

        _imageParameters = new PdfImageRowDecodingParameters(
            Context,
            header.Width,
            header.Height,
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

    /// <summary>
    /// Reads successive JPEG rows and returns completed tiles as each tile boundary is crossed.
    /// Returns null once all image rows have been consumed.
    /// </summary>
    /// <param name="observer">Observer notified after each decoded row; may be null.</param>
    /// <returns>One or more completed <see cref="PdfImageTile"/> instances, or null when decoding is finished.</returns>
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

    /// <summary>
    /// Releases all transient decode state (row decoder, buffers, tiling context) accumulated
    /// during a single <see cref="Initialize"/> / <see cref="DecodeNextTiles"/> pass.
    /// </summary>
    public override void Cleanup()
    {
        _jpgRowDecoder = null;
        _fullWidthRowBuffer = null;
        _tilingContext = null;
        _imageParameters = null;
        _currentImageRow = 0;
    }
}
