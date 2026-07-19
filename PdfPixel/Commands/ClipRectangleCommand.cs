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
    public ClipRectangleCommand(SKRect rect, SKClipOperation operation)
    {
        Rect = rect;
        Operation = operation;
    }

    /// <summary>
    /// Initializes the command with the given rectangle and clip operation.
    /// </summary>
    public ClipRectangleCommand(in PdfRectangle rect, PdfClipOperation operation)
        : this(rect.ToSkRect(), SkiaEnumUtilities.ToSkClipOperation(operation))
    {
    }

    /// <summary>
    /// Gets the rectangle applied as a clip to the canvas.
    /// </summary>
    public SKRect Rect { get; }

    /// <summary>
    /// Gets the clip operation applied to the canvas.
    /// </summary>
    public SKClipOperation Operation { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        SKRect snappedRect = CommandHelpers.GetPixelSnappedRect(Rect, executionContext);
        bool antialias = CommandHelpers.GetRectIsAntialias(snappedRect, executionContext);
        executionContext.Canvas.ClipRect(snappedRect, Operation, antialias);
        executionContext.Frames.OnClipRect(snappedRect, Operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
