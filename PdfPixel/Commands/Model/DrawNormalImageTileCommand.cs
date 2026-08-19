using PdfPixel.Commands.Image;
using System.Globalization;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Represents one tile of an image with no mask, placed directly with no blending.
/// </summary>
public sealed class DrawNormalImageTileCommand : PdfCommand
{
    internal DrawNormalImageTileCommand(NormalImageExecutionContext context, int tileIndex)
    {
        Context = context;
        TileIndex = tileIndex;
    }

    /// <summary>
    /// The context this command was constructed with.
    /// </summary>
    public NormalImageExecutionContext Context { get; }

    /// <summary>
    /// The index of the tile this command draws.
    /// </summary>
    public int TileIndex { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawNormalImageTile;

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(DrawNormalImageTileCommand)} tile={TileIndex.ToString(CultureInfo.InvariantCulture)}";
}
