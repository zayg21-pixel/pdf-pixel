using PdfPixel.Geometry;
using PdfPixel.Models;

namespace PdfPixel.Commands;

/// <summary>
/// Represents a rectangular clip operation.
/// </summary>
public sealed class ClipRectangleCommand : PdfCommand
{
    /// <summary>
    /// Initializes the command with the given rectangle and clip operation.
    /// </summary>
    public ClipRectangleCommand(in PdfRectangle rect, PdfClipOperation operation)
    {
        Rect = rect;
        Operation = operation;
    }

    /// <summary>
    /// Gets the rectangle this command clips to.
    /// </summary>
    public PdfRectangle Rect { get; }

    /// <summary>
    /// Gets the clip operation this command applies.
    /// </summary>
    public PdfClipOperation Operation { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.ClipRectangle;

}
