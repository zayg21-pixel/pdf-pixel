using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Models;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

internal sealed class PdfImageTilingContext
{
    private readonly PdfTileInfo _tileInfo;
    private readonly PdfImageRowProcessor _rowProcessor;

    private readonly IndexRange[] _columnSampleRanges;
    private readonly IndexRange[] _rowSampleRanges;
    private readonly IndexRange[] _columnOutputRanges;
    private readonly IndexRange[] _rowOutputRanges;
    private readonly HashSet<int>? _tileIndexesToDecode;

    private readonly List<OpenTileRow> _openTileRows = [];

    private int _nextTileRowToOpen;

    public PdfImageTilingContext(
        PdfTileInfo tileInfo,
        PdfImageRowDecodingParameters imageParameters,
        in PdfIntegerSize outputSize,
        HashSet<int>? tileIndexesToDecode,
        ILoggerFactory loggerFactory)
    {
        _tileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));

        if (imageParameters == null)
        {
            throw new ArgumentNullException(nameof(imageParameters));
        }

        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        _tileIndexesToDecode = tileIndexesToDecode;

        _columnSampleRanges = ComputeSampleRanges(tileInfo.TilesHorizontal, tileInfo.TileWidth, tileInfo.ImageSize.Width, imageParameters.Width);
        _rowSampleRanges = ComputeSampleRanges(tileInfo.TilesVertical, tileInfo.TileHeight, tileInfo.ImageSize.Height, imageParameters.Height);

        // The output ranges partition the output grid the same way, so the tiles cover it exactly
        // however far it sits from the decoded grid, and a tile boundary falls on the same place in both.
        _columnOutputRanges = ComputeSampleRanges(tileInfo.TilesHorizontal, tileInfo.TileWidth, tileInfo.ImageSize.Width, outputSize.Width);
        _rowOutputRanges = ComputeSampleRanges(tileInfo.TilesVertical, tileInfo.TileHeight, tileInfo.ImageSize.Height, outputSize.Height);

        _rowProcessor = new PdfImageRowProcessor(imageParameters, outputSize, loggerFactory.CreateLogger<PdfImageRowProcessor>());
    }

    /// <summary>
    /// Writes one fully-decoded image row, advancing every tile row whose sampled range
    /// includes <paramref name="imageRowIndex"/>, and returns the tiles of any tile rows
    /// that complete as a result (or null if none completed yet).
    /// </summary>
    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    public PdfImageTile[]? WriteRowAndTryGetTiles(
        int imageRowIndex,
        in ReadOnlySpan<byte> fullWidthRow,
        IAlphaRowSource? alphaSource,
        IPdfExecutionObserver? observer)
    {
        OpenNewTileRows(imageRowIndex, observer);
        WriteRowToOpenTileRows(imageRowIndex, fullWidthRow, alphaSource, observer);
        return CloseFinishedTileRows(imageRowIndex, observer);
    }

    /// <summary>
    /// Opens every tile row whose sampled range starts at <paramref name="imageRowIndex"/>.
    /// Several tile rows can legitimately start on the same image row when the decoded
    /// resolution is lower than the tile grid — they then sample (and share) that single row.
    /// </summary>
    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    private void OpenNewTileRows(int imageRowIndex, IPdfExecutionObserver? observer)
    {
        while (_nextTileRowToOpen < _tileInfo.TilesVertical && _rowSampleRanges[_nextTileRowToOpen].Start == imageRowIndex)
        {
            int tileRow = _nextTileRowToOpen;
            var targets = new PdfImageRowTarget?[_tileInfo.TilesHorizontal];
            IndexRange rowRange = _rowSampleRanges[tileRow];
            IndexRange rowOutputRange = _rowOutputRanges[tileRow];

            for (int column = 0; column < _tileInfo.TilesHorizontal; column++)
            {
                int tileIndex = (tileRow * _tileInfo.TilesHorizontal) + column;

                if (_tileIndexesToDecode != null && !_tileIndexesToDecode.Contains(tileIndex))
                {
                    continue;
                }

                IndexRange columnRange = _columnSampleRanges[column];
                IndexRange columnOutputRange = _columnOutputRanges[column];

                PdfIntegerRectangle sourceRegion = new(columnRange.Start, rowRange.Start, columnRange.End, rowRange.End);
                PdfIntegerRectangle outputRegion = new(columnOutputRange.Start, rowOutputRange.Start, columnOutputRange.End, rowOutputRange.End);

                targets[column] = _rowProcessor.CreateTarget(sourceRegion, outputRegion);

                observer?.Notify();
            }

            _openTileRows.Add(new OpenTileRow(tileRow, targets));
            _nextTileRowToOpen++;
        }
    }

    [MethodImpl(methodImplOptions:  MethodImplOptions.AggressiveInlining)]
    private void WriteRowToOpenTileRows(
        int imageRowIndex,
        in ReadOnlySpan<byte> fullWidthRow,
        IAlphaRowSource? alphaSource,
        IPdfExecutionObserver? observer)
    {
        foreach (OpenTileRow openTileRow in _openTileRows)
        {
            int rowWithinTile = imageRowIndex - _rowSampleRanges[openTileRow.TileRow].Start;
            _rowProcessor.DecodeRow(rowWithinTile, fullWidthRow, alphaSource, openTileRow.Targets, observer);
        }
    }

    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
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

    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
    private void EmitTiles(OpenTileRow openTileRow, List<PdfImageTile> destination)
    {
        for (int column = 0; column < _tileInfo.TilesHorizontal; column++)
        {
            int tileIndex = (openTileRow.TileRow * _tileInfo.TilesHorizontal) + column;
            PdfImageRowTarget? target = openTileRow.Targets[column];

            if (target == null)
            {
                destination.Add(PdfImageTile.CreateEmpty(tileIndex));
                continue;
            }

            openTileRow.Targets[column] = null;

            destination.Add(new PdfImageTile(tileIndex, _tileInfo.GetTilePosition(tileIndex), target.Image));
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
    [MethodImpl(methodImplOptions: MethodImplOptions.AggressiveInlining)]
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
        public OpenTileRow(int tileRow, PdfImageRowTarget?[] targets)
        {
            TileRow = tileRow;
            Targets = targets;
        }

        public int TileRow { get; }

        public PdfImageRowTarget?[] Targets { get; }
    }
}
