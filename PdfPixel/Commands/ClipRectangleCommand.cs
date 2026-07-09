using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a rectangular clip to the canvas.
/// </summary>
public sealed class ClipRectangleCommand : PdfCommand
{
    private readonly SKRect _rect;
    private readonly SKClipOperation _operation;

    /// <summary>
    /// Initializes the command with the given rectangle and clip operation.
    /// </summary>
    public ClipRectangleCommand(SKRect rect, SKClipOperation operation)
    {
        _rect = rect;
        _operation = operation;
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        SKRect snappedRect = CommandHelpers.GetPixelSnappedRect(_rect, executionContext);
        bool antialias = CommandHelpers.GetRectIsAntialias(snappedRect, executionContext);
        executionContext.Canvas.ClipRect(snappedRect, _operation, antialias);
        executionContext.Frames.OnClipRect(snappedRect, _operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
