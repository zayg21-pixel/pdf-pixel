using PdfPixel.Commands.Image;
using PdfPixel.Geometry;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Initializes the tile cache for an image before its per-tile draw commands run, computing the
/// region of interest to decode.
/// </summary>
public sealed class InitializeTileCacheCommand : PdfCommand
{
    internal InitializeTileCacheCommand(PdfImageTileCacheEntry tileCache, in PdfIntegerSize imageSize)
    {
        TileCache = tileCache;
        ImageSize = imageSize;
    }

    /// <summary>
    /// The tile cache this command initializes.
    /// </summary>
    public PdfImageTileCacheEntry TileCache { get; }

    /// <summary>
    /// The full pixel size of the image the tile cache decodes.
    /// </summary>
    public PdfIntegerSize ImageSize { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.InitializeTileCache;

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(InitializeTileCacheCommand)} {TileCache.Decoder.Image.Type}, {ImageSize.Width}x{ImageSize.Height}, {TileCache.Decoder.Image.BitsPerComponent}bpc, {TileCache.TileInfo.TotalTiles} tiles";
}
