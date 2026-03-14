using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a rectangular clip region to the canvas.
/// </summary>
public sealed class ClipRectCommand : PdfCommand
{
    private readonly SKRect _rect;
    private readonly SKClipOperation _operation;
    private readonly bool _antialias;

    public ClipRectCommand(SKRect rect, SKClipOperation operation, bool antialias)
    {
        _rect = rect;
        _operation = operation;
        _antialias = antialias;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers)
    {
        canvas.ClipRect(_rect, _operation, _antialias);
    }
}
