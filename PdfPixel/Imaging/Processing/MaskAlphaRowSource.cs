using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Produces the alpha plane rows of an image's mask, resampled to the output grid the image
/// the mask applies to is produced on.
/// </summary>
internal sealed class MaskAlphaRowSource : IAlphaRowSource
{
    private readonly PdfImageTileDecodingPass _pass;
    private readonly PdfTileInfo _tileInfo;
    private readonly Dictionary<int, PdfDecodedImage> _tiles = [];

    private PdfIntegerSize _outputSize;
    private HashSet<int>? _tileIndexesToDecode;
    private IPdfExecutionObserver? _observer;
    private bool _isPassPending;
    private int _remainingTileCount;
    private byte[]? _unpackedRow;

    public MaskAlphaRowSource(PdfImageDecoder decoder, PdfTileInfo tileInfo, ILoggerFactory loggerFactory)
    {
        _pass = new PdfImageTileDecodingPass(decoder, tileInfo, loggerFactory);
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
    public void Initialize(in PdfIntegerSize outputSize, HashSet<int>? tileIndexesToDecode, IPdfExecutionObserver? observer)
    {
        Cleanup();

        _outputSize = outputSize;
        _tileIndexesToDecode = tileIndexesToDecode;
        _observer = observer;
        _remainingTileCount = tileIndexesToDecode?.Count ?? _tileInfo.TotalTiles;
        _isPassPending = true;
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetRow(PdfImageRowTarget target)
    {
        int tileIndex = target.TileIndex;

        if (!_tiles.TryGetValue(tileIndex, out PdfDecodedImage? tile))
        {
            DecodeUntilProduced(tileIndex);

            if (!_tiles.TryGetValue(tileIndex, out tile))
            {
                return default;
            }
        }

        int rowWithinTile = target.CurrentOutputRow - target.OutputRowStart;

        if (rowWithinTile >= tile.Height)
        {
            return default;
        }

        if (rowWithinTile == tile.Height - 1)
        {
            _tiles.Remove(tileIndex);
        }

        ReadOnlySpan<byte> maskRow = tile.GetRow(rowWithinTile);

        if (tile.ColorFormat != PdfImageColorFormat.Rgba)
        {
            return maskRow;
        }

        if (_unpackedRow == null || _unpackedRow.Length < tile.Width)
        {
            _unpackedRow = new byte[tile.Width];
        }

        Span<byte> unpackedRow = _unpackedRow.AsSpan(0, tile.Width);
        UnpackRow(maskRow, unpackedRow);

        return unpackedRow;
    }

    public void Cleanup()
    {
        _pass.End();
        _tiles.Clear();
        _tileIndexesToDecode = null;
        _observer = null;
        _isPassPending = false;
        _remainingTileCount = 0;
        _unpackedRow = null;
    }

    private void DecodeUntilProduced(int tileIndex)
    {
        if (_isPassPending)
        {
            // A transformation covering the output extent asks the decoder for the largest reduction
            // that still fills it; a mask no larger than the output reports no reduction at all.
            PdfMatrix outputExtent = PdfMatrix.CreateScale(_outputSize.Width, _outputSize.Height);
            _pass.Begin(outputExtent, _outputSize, _tileIndexesToDecode, _observer);
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

        return new MaskAlphaRowSource(maskDecoder, tileInfo, loggerFactory);
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private static void UnpackRow(in ReadOnlySpan<byte> maskRow, in Span<byte> destination)
    {
        for (int x = 0; x < destination.Length; x++)
        {
            destination[x] = maskRow[x * 4];
        }
    }
}
