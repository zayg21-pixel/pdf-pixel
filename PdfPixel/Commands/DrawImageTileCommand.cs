using PdfPixel.Commands.Image;
using System.Globalization;

namespace PdfPixel.Commands;

/// <summary>
/// Draws one tile of an image, using the fill paint and compositing captured in its decoding context.
/// </summary>
public sealed class DrawImageTileCommand : PdfCommand
{
    internal DrawImageTileCommand(PdfImageTileCacheEntry tileCache, int tileIndex)
    {
        TileCache = tileCache;
        TileIndex = tileIndex;
    }

    /// <summary>
    /// The tile cache producing the tile this command draws.
    /// </summary>
    internal PdfImageTileCacheEntry TileCache { get; }

    /// <summary>
    /// The index of the tile this command draws.
    /// </summary>
    public int TileIndex { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawImageTile;

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(DrawImageTileCommand)} tile={TileIndex.ToString(CultureInfo.InvariantCulture)}";
}
