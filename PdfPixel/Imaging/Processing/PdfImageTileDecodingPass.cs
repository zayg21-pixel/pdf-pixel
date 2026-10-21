using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using System;
using System.Collections.Generic;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Decodes the tiles of an image on a tile grid, a tile row at a time, from the rows its decoder produces.
/// </summary>
internal sealed class PdfImageTileDecodingPass
{
    private readonly PdfTileInfo _tileInfo;
    private readonly ILoggerFactory _loggerFactory;

    private PdfImageTilingContext? _tilingContext;
    private PdfImageRowDecodingParameters? _parameters;
    private byte[]? _rowBuffer;
    private int _currentRow;

    public PdfImageTileDecodingPass(PdfImageDecoder decoder, PdfTileInfo tileInfo, ILoggerFactory loggerFactory)
    {
        Decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _tileInfo = tileInfo ?? throw new ArgumentNullException(nameof(tileInfo));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Decoder the rows are read from.
    /// </summary>
    public PdfImageDecoder Decoder { get; }

    /// <summary>
    /// True between <see cref="Begin"/> and <see cref="End"/>.
    /// </summary>
    public bool IsActive => _tilingContext != null;

    /// <summary>
    /// Starts a pass producing the tiles in <paramref name="tileIndexesToDecode"/> on <paramref name="outputSize"/>,
    /// or every tile when it is null.
    /// </summary>
    public void Begin(in PdfMatrix ctm, in PdfIntegerSize outputSize, HashSet<int>? tileIndexesToDecode, IPdfExecutionObserver? observer)
    {
        End();

        _parameters = Decoder.Initialize(ComputeRegionsOfInterest(tileIndexesToDecode), ctm, observer);
        _tilingContext = new PdfImageTilingContext(_tileInfo, _parameters, outputSize, tileIndexesToDecode, _loggerFactory);
        _rowBuffer = new byte[_parameters.RowBytes];
    }

    /// <summary>
    /// Reads decoded rows into the tiling context until a tile row completes, and returns the tiles
    /// it produced. Returns null once the decoder stops before the grid is complete.
    /// </summary>
    public PdfImageTile[]? DecodeNextTiles(IAlphaRowSource? alphaSource, IPdfExecutionObserver? observer)
    {
        if (_parameters == null || _tilingContext == null || _rowBuffer == null)
        {
            return null;
        }

        while (_currentRow < _parameters.Height)
        {
            if (!Decoder.TryReadNextRow(_rowBuffer, observer))
            {
                return null;
            }

            PdfImageTile[]? tiles = _tilingContext.WriteRowAndTryGetTiles(_currentRow, _rowBuffer, alphaSource, observer);
            _currentRow++;
            observer?.Notify();

            if (tiles != null)
            {
                return tiles;
            }
        }

        return null;
    }

    /// <summary>
    /// Releases the decoder state and the buffers the pass reconstructs through.
    /// </summary>
    public void End()
    {
        if (_tilingContext != null)
        {
            Decoder.Cleanup();
        }

        _tilingContext = null;
        _parameters = null;
        _rowBuffer = null;
        _currentRow = 0;
    }

    /// <summary>
    /// Maps the requested tiles from the tile grid's image extent onto the decoder's image extent.
    /// </summary>
    private List<PdfIntegerRectangle>? ComputeRegionsOfInterest(HashSet<int>? tileIndexesToDecode)
    {
        if (tileIndexesToDecode == null)
        {
            return null;
        }

        PdfIntegerSize gridSize = _tileInfo.ImageSize;
        double scaleX = (double)Decoder.Image.Width / gridSize.Width;
        double scaleY = (double)Decoder.Image.Height / gridSize.Height;

        List<PdfIntegerRectangle> regionsOfInterest = new(tileIndexesToDecode.Count);
        foreach (int tileIndex in tileIndexesToDecode)
        {
            PdfIntegerRectangle tilePosition = _tileInfo.GetTilePosition(tileIndex);

            regionsOfInterest.Add(new PdfIntegerRectangle(
                (int)Math.Floor(tilePosition.Left * scaleX),
                (int)Math.Floor(tilePosition.Top * scaleY),
                (int)Math.Ceiling(tilePosition.Right * scaleX),
                (int)Math.Ceiling(tilePosition.Bottom * scaleY)));
        }

        return regionsOfInterest;
    }
}
