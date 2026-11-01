using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing.Pipeline;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Produces the alpha plane rows of an image's mask, resampled to the output grid the image
/// the mask applies to is produced on.
/// </summary>
internal sealed class MaskAlphaRowSource
{
    private readonly PdfImageTileDecodingPass _pass;
    private readonly PdfTileInfo _tileInfo;
    private readonly Dictionary<int, PdfImageSamples> _tiles = [];

    private PdfIntegerSize _outputSize;
    private HashSet<int>? _tileIndexesToDecode;
    private IPdfImagePipelineFactory? _pipelineFactory;
    private IPdfExecutionObserver? _observer;
    private bool _isPassPending;
    private int _remainingTileCount;

    public MaskAlphaRowSource(PdfImageDecoder decoder, PdfTileInfo tileInfo)
    {
        _pass = new PdfImageTileDecodingPass(decoder, tileInfo);
        _tileInfo = tileInfo;
    }

    /// <summary>
    /// Sample grid the mask itself carries, which is what the image it applies to is produced on
    /// when the mask is the finer of the two.
    /// </summary>
    public PdfIntegerSize SampleSize => new(_pass.Decoder.Image.Width, _pass.Decoder.Image.Height);

    /// <summary>
    /// Prepares the mask to serve the rows of the tiles in <paramref name="tileIndexesToDecode"/> on
    /// <paramref name="outputSize"/>, or of every tile when it is null.
    /// </summary>
    public void Initialize(in PdfIntegerSize outputSize, HashSet<int>? tileIndexesToDecode, IPdfImagePipelineFactory pipelineFactory, IPdfExecutionObserver? observer)
    {
        Cleanup();

        _outputSize = outputSize;
        _tileIndexesToDecode = tileIndexesToDecode;
        _pipelineFactory = pipelineFactory ?? throw new ArgumentNullException(nameof(pipelineFactory));
        _observer = observer;
        _remainingTileCount = tileIndexesToDecode?.Count ?? _tileInfo.TotalTiles;
        _isPassPending = true;
    }

    /// <summary>
    /// Returns the source serving the alpha rows of the tile at <paramref name="tileIndex"/>.
    /// </summary>
    public IPdfAlphaRowSource GetTileSource(int tileIndex) => new TileAlphaRowSource(this, tileIndex);

    public void Cleanup()
    {
        _pass.End();
        _tiles.Clear();
        _tileIndexesToDecode = null;
        _pipelineFactory = null;
        _observer = null;
        _isPassPending = false;
        _remainingTileCount = 0;
    }

    private ReadOnlySpan<float> GetRow(int tileIndex, int rowIndex)
    {
        if (!_tiles.TryGetValue(tileIndex, out PdfImageSamples? tile))
        {
            DecodeUntilProduced(tileIndex);

            if (!_tiles.TryGetValue(tileIndex, out tile))
            {
                return default;
            }
        }

        if (rowIndex >= tile.Height)
        {
            return default;
        }

        if (rowIndex == tile.Height - 1)
        {
            _tiles.Remove(tileIndex);
        }

        return MemoryMarshal.Cast<byte, float>(tile.GetRow(rowIndex));
    }

    private void DecodeUntilProduced(int tileIndex)
    {
        if (_pipelineFactory == null)
        {
            return;
        }

        if (_isPassPending)
        {
            // A transformation covering the output extent asks the decoder for the largest reduction
            // that still fills it; a mask no larger than the output reports no reduction at all.
            PdfMatrix outputExtent = PdfMatrix.CreateScale(_outputSize.Width, _outputSize.Height);
            _pass.Begin(outputExtent, _outputSize, _tileIndexesToDecode, _pipelineFactory, producesMask: true, _observer);
            _isPassPending = false;
        }

        while (_pass.IsActive)
        {
            PdfImageTile[]? batch = _pass.DecodeNextTiles(alphaSource: null, _observer);

            if (batch == null)
            {
                _pass.End();
                return;
            }

            var producedRequestedTile = false;

            foreach (PdfImageTile tile in batch)
            {
                if (tile.Image == null)
                {
                    continue;
                }

                _tiles[tile.TileIndex] = tile.Image;
                _remainingTileCount--;

                if (tile.TileIndex == tileIndex)
                {
                    producedRequestedTile = true;
                }
            }

            if (_remainingTileCount <= 0)
            {
                _pass.End();
            }

            if (producedRequestedTile)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Creates the alpha source for the mask <paramref name="decoder"/> applies, or null when it applies none.
    /// </summary>
    public static MaskAlphaRowSource? Create(PdfImageDecoder decoder, PdfTileInfo tileInfo, ILoggerFactory loggerFactory)
    {
        PdfImage? mask = decoder.AppliedMask;
        if (mask == null)
        {
            return null;
        }

        ImageDecodingContext context = decoder.Context;
        ImageDecodingContext maskContext = new(context, mask, context.FillPaint);
        PdfImageDecoder? maskDecoder = PdfImageDecoder.GetDecoder(mask, maskContext, loggerFactory);

        if (maskDecoder == null)
        {
            throw new ArgumentException($"Mask decoder for image {mask.Type} is not defined.");
        }

        return new MaskAlphaRowSource(maskDecoder, tileInfo);
    }

    private sealed class TileAlphaRowSource : IPdfAlphaRowSource
    {
        private readonly MaskAlphaRowSource _mask;
        private readonly int _tileIndex;

        public TileAlphaRowSource(MaskAlphaRowSource mask, int tileIndex)
        {
            _mask = mask;
            _tileIndex = tileIndex;
        }

        public ReadOnlySpan<float> GetRow(int rowIndex) => _mask.GetRow(_tileIndex, rowIndex);
    }
}
