using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a rectangular clip region to the canvas.
/// </summary>
public sealed class ClipRectCommand : PdfCommand
{
    private readonly SKRect _rect;
    private readonly SKClipOperation _operation;

    public ClipRectCommand(SKRect rect, SKClipOperation operation)
    {
        _rect = rect;
        _operation = operation;
    }

    /// <inheritdoc />
    public override Task ExecuteAsync(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        canvas.ClipRect(_rect, _operation, antialias: executionContext.RenderingParameters.Antialias);
        return Task.CompletedTask;
    }
}
