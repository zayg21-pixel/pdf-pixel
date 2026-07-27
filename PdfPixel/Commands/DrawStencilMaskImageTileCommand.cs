using PdfPixel.Commands.Image;
using System.Globalization;

namespace PdfPixel.Commands;

/// <summary>
/// Represents one tile of an image mask (1-bit stencil), blending the fill color through the stencil.
/// </summary>
public sealed class DrawStencilMaskImageTileCommand : PdfCommand
{
    internal DrawStencilMaskImageTileCommand(StencilMaskImageExecutionContext context, int tileIndex)
    {
        Context = context;
        TileIndex = tileIndex;
    }

    /// <summary>
    /// The context this command was constructed with.
    /// </summary>
    public StencilMaskImageExecutionContext Context { get; }

    /// <summary>
    /// The index of the tile this command draws.
    /// </summary>
    public int TileIndex { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawStencilMaskImageTile;

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(DrawStencilMaskImageTileCommand)} tile={TileIndex.ToString(CultureInfo.InvariantCulture)}";
}
