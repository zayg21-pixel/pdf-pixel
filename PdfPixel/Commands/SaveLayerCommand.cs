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
    private readonly SKRect _bounds;
    private readonly SKPaint? _paint;

    /// <summary>
    /// Initializes the command with the layer bounds and takes ownership of the paint.
    /// </summary>
    public SaveLayerCommand(SKRect bounds, SKPaint? paint)
    {
        _bounds = bounds;
        _paint = paint;
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        if (_paint != null)
        {
            using SKPaint paint = _paint.Clone();
            paint.IsAntialias = executionContext.Parameters.Antialias;
            executionContext.Canvas.SaveLayer(_bounds, paint);
        }
        else
        {
            executionContext.Canvas.SaveLayer(_bounds, null);
        }

        executionContext.Frames.OnSaveLayer();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _paint?.Dispose();
}
