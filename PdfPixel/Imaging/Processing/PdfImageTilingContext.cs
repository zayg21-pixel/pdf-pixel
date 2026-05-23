using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

internal sealed class PdfImageTilingContext : IDisposable
{
    private readonly PdfImageRowDecodingParameters _imageParameters;
    private readonly ILoggerFactory _loggerFactory;

    private readonly SKRectI[] _tilePositions;
    private PdfImageRowProcessor[] _tileRowProcessors;
    private readonly SKMatrix _ctm;

    public PdfImageTilingContext(
        SKSizeI tileSize,
        PdfImageRowDecodingParameters imageParameters,
        SKMatrix ctm,
        SKRectI regionOfInterest,
        ILoggerFactory loggerFactory)
    {
        _imageParameters = imageParameters ?? throw new ArgumentNullException(nameof(imageParameters));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        RegionOfInterest = regionOfInterest;
        _ctm = ctm;

        int descale = imageParameters.DescaleFactor;
        TileWidth = Math.Min(tileSize.Width / descale, imageParameters.Width);
        TileHeight = Math.Min(tileSize.Height / descale, imageParameters.Height);

        TilesHorizontal = (imageParameters.Width + TileWidth - 1) / TileWidth;
        TilesVertical = (imageParameters.Height + TileHeight - 1) / TileHeight;
        TotalTiles = TilesHorizontal * TilesVertical;

        _tilePositions = BuildTilePositions();
    }

    public int TileWidth { get; }
    public int TileHeight { get; }
    public int TilesHorizontal { get; }
    public int TilesVertical { get; }
    public int TotalTiles { get; }
    public SKRectI RegionOfInterest { get; }

    public PdfImageTile[] WriteRowAndTryGetTiles(int imageRowIndex, ReadOnlySpan<byte> fullWidthRow, IPdfExecutionObserver observer)
    {
        int rowWithinTile = imageRowIndex % TileHeight;
        int tileRow = imageRowIndex / TileHeight;

        if (rowWithinTile == 0)
        {
            DisposeTileRowProcessors();
            _tileRowProcessors = new PdfImageRowProcessor[TilesHorizontal];

            for (int col = 0; col < TilesHorizontal; col++)
            {
                int tileIndex = tileRow * TilesHorizontal + col;
                if (tileIndex >= TotalTiles) break;

                SKRectI pos = _tilePositions[tileIndex];
                if (!pos.IntersectsWith(RegionOfInterest)) continue;
                var downscaledSize = PdfImageRowDecodingParameters.ComputeDownscaledSize( pos.Width, pos.Height, _imageParameters.ColorSpaceConverter, _imageParameters.Context, _ctm);

                var tileParams = new PdfImageRowDecodingParameters(
                    _imageParameters.Context,
                    pos.Width, pos.Height, _imageParameters.BitsPerComponent,
                    _imageParameters.RenderingIntent, _imageParameters.ColorSpaceConverter,
                    _imageParameters.HasImageMask, _imageParameters.MaskArray, _imageParameters.DecodeArray,
                    downscaledSize: downscaledSize, descaleFactor: 1);

                var processor = new PdfImageRowProcessor(tileParams, _loggerFactory.CreateLogger<PdfImageRowProcessor>());
                processor.InitializeBuffer();
                _tileRowProcessors[col] = processor;

                observer?.Notify();
            }
        }

        int componentCount = _imageParameters.ColorSpaceConverter.Components;
        int bpc = _imageParameters.BitsPerComponent;

        for (int col = 0; col < TilesHorizontal; col++)
        {
            if (_tileRowProcessors[col] == null) continue;
            int tileStartPixel = col * TileWidth;
            int tileActualWidth = _tilePositions[tileRow * TilesHorizontal + col].Width;
            byte[] slice = ExtractTileRowSlice(fullWidthRow, tileStartPixel, tileActualWidth, bpc, componentCount);
            _tileRowProcessors[col].WriteRow(rowWithinTile, slice);
            observer?.Notify();
        }

        bool isLastRowOfTile = rowWithinTile == TileHeight - 1 || imageRowIndex == _imageParameters.Height - 1;
        if (!isLastRowOfTile) return null;

        int tilesInRow = Math.Min(TilesHorizontal, TotalTiles - tileRow * TilesHorizontal);
        var tiles = new PdfImageTile[tilesInRow];

        for (int col = 0; col < tilesInRow; col++)
        {
            int tileIndex = tileRow * TilesHorizontal + col;
            if (_tileRowProcessors[col] == null)
            {
                tiles[col] = new PdfImageTile(tileIndex, ScaleTilePosition(_tilePositions[tileIndex]), null, isSkipped: true);
                continue;
            }
            SKImage image = _tileRowProcessors[col].GetDecoded();
            _tileRowProcessors[col].Dispose();
            _tileRowProcessors[col] = null;
            tiles[col] = new PdfImageTile(tileIndex, ScaleTilePosition(_tilePositions[tileIndex]), image, isSkipped: false);

            observer?.Notify();
        }

        return tiles;
    }

    private SKRectI[] BuildTilePositions()
    {
        var positions = new SKRectI[TotalTiles];
        for (int i = 0; i < TotalTiles; i++)
        {
            int col = i % TilesHorizontal;
            int row = i / TilesHorizontal;
            int x = col * TileWidth;
            int y = row * TileHeight;
            positions[i] = SKRectI.Create(
                x, y,
                Math.Min(TileWidth, _imageParameters.Width - x),
                Math.Min(TileHeight, _imageParameters.Height - y));
        }
        return positions;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SKRectI ScaleTilePosition(SKRectI pos)
    {
        int f = _imageParameters.DescaleFactor;
        return SKRectI.Create(pos.Left * f, pos.Top * f, pos.Width * f, pos.Height * f);
    }

    private static byte[] ExtractTileRowSlice(
        ReadOnlySpan<byte> fullWidthRow,
        int tileStartPixel,
        int tilePixelWidth,
        int bitsPerComponent,
        int componentCount)
    {
        int startBit = tileStartPixel * componentCount * bitsPerComponent;
        int totalBits = tilePixelWidth * componentCount * bitsPerComponent;
        int byteCount = (totalBits + 7) / 8;
        byte[] tileSlice = new byte[byteCount];

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
            byte topByte = (byte)(window >> 24);
            tileSlice[dstByteIdx++] = bitsThisByte == 8
                ? topByte
                : (byte)(topByte & (0xFF << (8 - bitsThisByte)));

            window <<= bitsThisByte;
            windowBits -= bitsThisByte;
            bitsRemaining -= bitsThisByte;
        }

        return tileSlice;
    }

    private void DisposeTileRowProcessors()
    {
        if (_tileRowProcessors == null) return;
        foreach (PdfImageRowProcessor p in _tileRowProcessors)
            p?.Dispose();
        _tileRowProcessors = null;
    }

    public void Dispose() => DisposeTileRowProcessors();
}
