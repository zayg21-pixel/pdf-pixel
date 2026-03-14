using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Saves a new layer on the canvas. Picks the right <c>canvas.SaveLayer</c> overload
/// depending on whether bounds and/or paint are supplied.
/// Owns the paint (if any) and disposes it with the command.
/// </summary>
public sealed class SaveLayerCommand : PdfCommand
{
    private readonly SKRect? _bounds;
    private readonly SKPaint _paint;

    public SaveLayerCommand()
    {
    }

    public SaveLayerCommand(SKPaint paint)
    {
        _paint = paint;
    }

    public SaveLayerCommand(SKRect bounds)
    {
        _bounds = bounds;
    }

    public SaveLayerCommand(SKRect bounds, SKPaint paint)
    {
        _bounds = bounds;
        _paint = paint;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers)
    {
        if (_bounds.HasValue)
        {
            canvas.SaveLayer(_bounds.Value, _paint);
        }
        else if (_paint != null)
        {
            canvas.SaveLayer(_paint);
        }
        else
        {
            canvas.SaveLayer();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _paint?.Dispose();
    }
}
