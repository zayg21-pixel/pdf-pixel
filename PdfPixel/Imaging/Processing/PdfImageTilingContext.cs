using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Processing;

internal sealed class PdfImageTilingContext : IDisposable
{
    private readonly PdfTileInfo _tileInfo;
    private readonly PdfImageRowDecodingParameters _imageParameters;
    private readonly ILoggerFactory _loggerFactory;

    private readonly IndexRange[] _columnSampleRanges;
    private readonly IndexRange[] _rowSampleRanges;
    private readonly HashSet<int>? _tileIndexesToDecode;

    private readonly List<OpenTileRow> _openTileRows = [];
    private readonly bool _isDownscaled;
    private readonly float _outputScaleX;
    private readonly float _outputScaleY;

    private int _nextTileRowToOpen;

    public PdfImageTilingContext(
        PdfTileInfo tileInfo,
        PdfImageRowDecodingParameters imageParameters,
        HashSet<int>? tileIndexesToDecode,
        ILoggerFactory loggerFactory)
    {
        _tileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));
        _imageParameters = imageParameters ?? throw new ArgumentNullException(nameof(imageParameters));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _tileIndexesToDecode = tileIndexesToDecode;

        if (imageParameters.DownscaledSize.HasValue)
        {
            _isDownscaled = true;
            _outputScaleX = (float)imageParameters.DownscaledSize.Value.Width / imageParameters.Width;
            _outputScaleY = (float)imageParameters.DownscaledSize.Value.Height / imageParameters.Height;
        }

        _columnSampleRanges = ComputeSampleRanges(tileInfo.TilesHorizontal, tileInfo.TileWidth, tileInfo.ImageSize.Width, imageParameters.Width);
        _rowSampleRanges = ComputeSampleRanges(tileInfo.TilesVertical, tileInfo.TileHeight, tileInfo.ImageSize.Height, imageParameters.Height);
    }

    /// <summary>
    /// Writes one fully-decoded image row, advancing every tile row whose sampled range
    /// includes <paramref name="imageRowIndex"/>, and returns the tiles of any tile rows
    /// that complete as a result (or null if none completed yet).
    /// </summary>
    public PdfImageTile[]? WriteRowAndTryGetTiles(int imageRowIndex, in ReadOnlySpan<byte> fullWidthRow, IPdfExecutionObserver? observer)
    {
        OpenNewTileRows(imageRowIndex, observer);
        WriteRowToOpenTileRows(imageRowIndex, fullWidthRow, observer);
        return CloseFinishedTileRows(imageRowIndex, observer);
    }

    /// <summary>
    /// Opens every tile row whose sampled range starts at <paramref name="imageRowIndex"/>.
    /// Several tile rows can legitimately start on the same image row when the decoded
    /// resolution is lower than the tile grid — they then sample (and share) that single row.
    /// </summary>
    private void OpenNewTileRows(int imageRowIndex, IPdfExecutionObserver? observer)
    {
        while (_nextTileRowToOpen < _tileInfo.TilesVertical && _rowSampleRanges[_nextTileRowToOpen].Start == imageRowIndex)
        {
            int tileRow = _nextTileRowToOpen;
            var processors = new PdfImageRowProcessor?[_tileInfo.TilesHorizontal];
            var tileParameters = new PdfImageRowDecodingParameters[_tileInfo.TilesHorizontal];
            IndexRange rowRange = _rowSampleRanges[tileRow];

            for (int column = 0; column < _tileInfo.TilesHorizontal; column++)
            {
                int tileIndex = (tileRow * _tileInfo.TilesHorizontal) + column;
                bool mustDecode = _tileIndexesToDecode == null || _tileIndexesToDecode.Contains(tileIndex);
                IndexRange columnRange = _columnSampleRanges[column];
                int decodedWidth = columnRange.End - columnRange.Start;
                int decodedHeight = rowRange.End - rowRange.Start;

                SKSizeI? downscaledSize = _isDownscaled
                    ? new SKSizeI(Math.Max(1, (int)Math.Floor(decodedWidth * _outputScaleX)), Math.Max(1, (int)Math.Floor(decodedHeight * _outputScaleY)))
                    : null;

                PdfImageRowDecodingParameters parameters = new(
                    _imageParameters.Context,
                    decodedWidth,
                    decodedHeight,
                    _imageParameters.BitsPerComponent,
                    _imageParameters.RenderingIntent,
                    _imageParameters.ColorSpaceConverter,
                    _imageParameters.HasImageMask,
                    _imageParameters.MaskArray,
                    _imageParameters.DecodeArray,
                    downscaledSize: downscaledSize,
                    hasAlphaChannel: _imageParameters.HasAlphaChannel);

                tileParameters[column] = parameters;

                if (!mustDecode)
                {
                    continue;
                }

                PdfImageRowProcessor processor = new(parameters, _loggerFactory.CreateLogger<PdfImageRowProcessor>());
                processor.InitializeBuffer();
                processors[column] = processor;

                observer?.Notify();
            }

            _openTileRows.Add(new OpenTileRow(tileRow, processors, tileParameters));
            _nextTileRowToOpen++;
        }
    }

    private void WriteRowToOpenTileRows(int imageRowIndex, in ReadOnlySpan<byte> fullWidthRow, IPdfExecutionObserver? observer)
    {
        int componentCount = _imageParameters.ColorSpaceConverter.Components
            + ((_imageParameters.HasAlphaChannel) ? 1 : 0);
        int bitsPerComponent = _imageParameters.BitsPerComponent;

        foreach (OpenTileRow openTileRow in _openTileRows)
        {
            int rowWithinTile = imageRowIndex - _rowSampleRanges[openTileRow.TileRow].Start;

            for (int column = 0; column < _tileInfo.TilesHorizontal; column++)
            {
                if (openTileRow.Processors[column] == null)
                {
                    continue;
                }

                IndexRange columnRange = _columnSampleRanges[column];
                byte[] slice = ExtractTileRowSlice(fullWidthRow, columnRange.Start, columnRange.End - columnRange.Start, bitsPerComponent, componentCount);
                openTileRow.Processors[column]?.WriteRow(rowWithinTile, slice);
                observer?.Notify();
            }
        }
    }

    private PdfImageTile[]? CloseFinishedTileRows(int imageRowIndex, IPdfExecutionObserver? observer)
    {
        List<PdfImageTile>? closedTiles = null;

        int writeIndex = 0;
        for (int readIndex = 0; readIndex < _openTileRows.Count; readIndex++)
        {
            OpenTileRow openTileRow = _openTileRows[readIndex];

            if (imageRowIndex != _rowSampleRanges[openTileRow.TileRow].End - 1)
            {
                _openTileRows[writeIndex++] = openTileRow;
                continue;
            }

            closedTiles ??= new List<PdfImageTile>();
            EmitTiles(openTileRow, closedTiles);
            observer?.Notify();
        }

        _openTileRows.RemoveRange(writeIndex, _openTileRows.Count - writeIndex);
        return closedTiles?.ToArray();
    }

    private void EmitTiles(OpenTileRow openTileRow, List<PdfImageTile> destination)
    {
        for (int column = 0; column < _tileInfo.TilesHorizontal; column++)
        {
            int tileIndex = (openTileRow.TileRow * _tileInfo.TilesHorizontal) + column;
            SKRectI tilePosition = _tileInfo.GetTilePosition(tileIndex);

            if (openTileRow.Processors[column] == null)
            {
                destination.Add(new PdfImageTile(tileIndex, tilePosition, null, null, isSkipped: true));
                continue;
            }

            SKImage? image = openTileRow.Processors[column]?.GetDecoded();
            openTileRow.Processors[column]?.Dispose();
            openTileRow.Processors[column] = null;
            destination.Add(new PdfImageTile(tileIndex, tilePosition, image, openTileRow.Parameters[column], isSkipped: false));
        }
    }

    /// <summary>
    /// Maps a fixed <paramref name="tileCount"/>-cell nominal grid — defined over
    /// <paramref name="nominalImageDimension"/> samples in steps of <paramref name="nominalTileSize"/> —
    /// onto sampled ranges within <paramref name="decodedImageDimension"/> decoded samples.
    /// Every cell receives a non-empty range that lies within the decoded extent: when the
    /// decoded resolution is lower than the cell count, several adjacent cells legitimately
    /// resolve to (and share) the same single decoded sample.
    /// </summary>
    private static IndexRange[] ComputeSampleRanges(int tileCount, int nominalTileSize, int nominalImageDimension, int decodedImageDimension)
    {
        float scale = (float)decodedImageDimension / nominalImageDimension;
        var ranges = new IndexRange[tileCount];
        int previousBoundary = 0;

        for (int i = 0; i < tileCount; i++)
        {
            int nextBoundary;

            if (i + 1 < tileCount)
            {
                int nominalBoundary = Math.Min((i + 1) * nominalTileSize, nominalImageDimension);
                var scaledBoundary = (int)Math.Round(nominalBoundary * scale);
                nextBoundary = Math.Max(previousBoundary, Math.Min(scaledBoundary, decodedImageDimension));
            }
            else
            {
                nextBoundary = decodedImageDimension;
            }

            int start = Math.Min(previousBoundary, decodedImageDimension - 1);
            int end = Math.Max(start + 1, Math.Min(nextBoundary, decodedImageDimension));
            ranges[i] = new IndexRange(start, end);
            previousBoundary = nextBoundary;
        }

        return ranges;
    }

    private static byte[] ExtractTileRowSlice(
        in ReadOnlySpan<byte> fullWidthRow,
        int tileStartPixel,
        int tilePixelWidth,
        int bitsPerComponent,
        int componentCount)
    {
        int startBit = tileStartPixel * componentCount * bitsPerComponent;
        int totalBits = tilePixelWidth * componentCount * bitsPerComponent;
        int byteCount = (totalBits + 7) / 8;
        var tileSlice = new byte[byteCount];

        int srcBitOffset = startBit & 7;
        if (srcBitOffset == 0)
        {
            fullWidthRow.Slice(startBit >> 3, byteCount).CopyTo(tileSlice);
            return tileSlice;
        }

        int srcByteIdx = startBit >> 3;
        uint window = 0;
        int windowBits = 0;

        while (windowBits <= 24 && srcByteIdx < fullWidthRow.Length)
        {
            window |= (uint)fullWidthRow[srcByteIdx++] << (24 - windowBits);
            windowBits += 8;
        }

        window <<= srcBitOffset;
        windowBits -= srcBitOffset;

        int bitsRemaining = totalBits;
        int dstByteIdx = 0;

        while (bitsRemaining > 0)
        {
            while (windowBits <= 24 && srcByteIdx < fullWidthRow.Length)
            {
                window |= (uint)fullWidthRow[srcByteIdx++] << (24 - windowBits);
                windowBits += 8;
            }

            int bitsThisByte = Math.Min(8, bitsRemaining);
            var topByte = (byte)(window >> 24);
            tileSlice[dstByteIdx++] = (bitsThisByte == 8)
                ? topByte
                : (byte)(topByte & (0xFF << (8 - bitsThisByte)));

            window <<= bitsThisByte;
            windowBits -= bitsThisByte;
            bitsRemaining -= bitsThisByte;
        }

        return tileSlice;
    }

    private void DisposeOpenTileRows()
    {
        foreach (OpenTileRow openTileRow in _openTileRows)
        {
            foreach (PdfImageRowProcessor? processor in openTileRow.Processors)
            {
                processor?.Dispose();
            }
        }

        _openTileRows.Clear();
    }

    public void Dispose() => DisposeOpenTileRows();

    private readonly struct IndexRange
    {
        public IndexRange(int start, int end)
        {
            Start = start;
            End = end;
        }

        public int Start { get; }

        public int End { get; }
    }

    private sealed class OpenTileRow
    {
        public OpenTileRow(int tileRow, PdfImageRowProcessor?[] processors, PdfImageRowDecodingParameters[] parameters)
        {
            TileRow = tileRow;
            Processors = processors;
            Parameters = parameters;
        }

        public int TileRow { get; }

        public PdfImageRowProcessor?[] Processors { get; }

        public PdfImageRowDecodingParameters[] Parameters { get; }
    }
}
