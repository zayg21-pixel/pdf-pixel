using PdfPixel.Commands.Image;
using System.Globalization;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Draws one tile of an image, using the fill paint and compositing captured in its context.
/// </summary>
public sealed class DrawImageTileCommand : PdfCommand
{
    internal DrawImageTileCommand(ImageExecutionContext context, int tileIndex)
    {
        Context = context;
        TileIndex = tileIndex;
    }

    /// <summary>
    /// The context this command was constructed with.
    /// </summary>
    public ImageExecutionContext Context { get; }

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
