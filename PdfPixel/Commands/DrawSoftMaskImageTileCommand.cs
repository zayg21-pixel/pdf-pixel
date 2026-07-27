using PdfPixel.Commands.Image;
using System.Globalization;

namespace PdfPixel.Commands;

/// <summary>
/// Represents one tile of an image combined with a soft mask image (/SMask), blended through the grayscale mask.
/// </summary>
public sealed class DrawSoftMaskImageTileCommand : PdfCommand
{
    internal DrawSoftMaskImageTileCommand(SoftMaskImageExecutionContext context, int tileIndex)
    {
        Context = context;
        TileIndex = tileIndex;
    }

    /// <summary>
    /// The context this command was constructed with.
    /// </summary>
    public SoftMaskImageExecutionContext Context { get; }

    /// <summary>
    /// The index of the image and mask tile this command draws.
    /// </summary>
    public int TileIndex { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Region | PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawSoftMaskImageTile;

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(DrawSoftMaskImageTileCommand)} tile={TileIndex.ToString(CultureInfo.InvariantCulture)}";
}
