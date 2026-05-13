using SkiaSharp;
using System;

namespace PdfPixel.Imaging.Processing;

public sealed class PdfTileInfo
{
    public PdfTileInfo(SKSizeI imageSize, SKSizeI tileSize)
    {
        TileWidth = Math.Min(tileSize.Width, imageSize.Width);
        TileHeight = Math.Min(tileSize.Height, imageSize.Height);
        TilesHorizontal = (imageSize.Width + TileWidth - 1) / TileWidth;
        TilesVertical = (imageSize.Height + TileHeight - 1) / TileHeight;
        TotalTiles = TilesHorizontal * TilesVertical;
    }

    public int TileWidth { get; }
    public int TileHeight { get; }
    public int TilesHorizontal { get; }
    public int TilesVertical { get; }
    public int TotalTiles { get; }
}
