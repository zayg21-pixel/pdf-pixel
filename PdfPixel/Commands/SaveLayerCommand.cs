using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Saves a new layer on the canvas. Picks the right <c>canvas.SaveLayer</c> overload
/// depending on whether bounds and/or paint are supplied.
/// Owns the paint (if any) and disposes it with the command.
/// </summary>
public sealed class SaveLayerCommand : PdfCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the layer bounds and takes ownership of the paint.
    /// </summary>
    public SaveLayerCommand(SKRect bounds, SKPaint? paint)
    {
        Bounds = bounds;
        Paint = paint;
    }

    /// <summary>
    /// Gets the bounds of the layer being saved.
    /// </summary>
    public SKRect Bounds { get; }

    /// <inheritdoc />
    public SKPaint? Paint { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        if (Paint != null)
        {
            using SKPaint paint = Paint.Clone();
            paint.IsAntialias = executionContext.Parameters.Antialias;
            executionContext.Canvas.SaveLayer(Bounds, paint);
        }
        else
        {
            executionContext.Canvas.SaveLayer(Bounds, null);
        }

        executionContext.Frames.OnSaveLayer();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => Paint?.Dispose();
}
