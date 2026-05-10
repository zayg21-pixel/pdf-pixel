using SkiaSharp;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Saves a new layer on the canvas. Picks the right <c>canvas.SaveLayer</c> overload
/// depending on whether bounds and/or paint are supplied.
/// Owns the paint (if any) and disposes it with the command.
/// </summary>
public sealed class SaveLayerCommand : PdfCommand
{
    private readonly SKRect _bounds;
    private readonly SKPaint _paint;

    public SaveLayerCommand(SKRect bounds, SKPaint paint)
    {
        _bounds = bounds;
        _paint = paint;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        if (_paint != null)
        {
            using var paint = _paint.Clone();
            paint.IsAntialias = executionContext.RenderingParameters.Antialias;
            canvas.SaveLayer(_bounds, paint);
        }
        else
        {
            canvas.SaveLayer(_bounds, null);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _paint?.Dispose();
    }
}
