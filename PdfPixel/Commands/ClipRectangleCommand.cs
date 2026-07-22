using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a rectangular clip to the canvas.
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
    /// Gets the rectangle applied as a clip to the canvas.
    /// </summary>
    public PdfRectangle Rect { get; }

    /// <summary>
    /// Gets the clip operation applied to the canvas.
    /// </summary>
    public PdfClipOperation Operation { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        SKRect snappedRect = CommandHelpers.GetPixelSnappedRect(Rect.ToSkRect(), executionContext);
        SKClipOperation skOperation = Operation.ToSkClipOperation();
        bool antialias = CommandHelpers.GetRectIsAntialias(snappedRect, executionContext);
        executionContext.Canvas.ClipRect(snappedRect, skOperation, antialias);
        executionContext.Frames.OnClipRect(Rect, Operation);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
