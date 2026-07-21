using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Saves a new layer on the canvas. Picks the right <c>canvas.SaveLayer</c> overload
/// depending on whether bounds and/or paint are supplied.
/// </summary>
public sealed class SaveLayerCommand : PdfCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the layer bounds and paint.
    /// </summary>
    public SaveLayerCommand(in PdfRectangle bounds, PdfPaint? paint = null)
    {
        Bounds = bounds;
        Paint = paint;
    }

    /// <summary>
    /// Gets the bounds of the layer being saved.
    /// </summary>
    public PdfRectangle Bounds { get; }

    /// <inheritdoc />
    public PdfPaint? Paint { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        SKRect skBounds = Bounds.ToSkRect();

        if (Paint != null)
        {
            using SKPaint paint = Paint.ToSkiaPaint();
            paint.IsAntialias = executionContext.Parameters.Antialias;
            executionContext.Canvas.SaveLayer(skBounds, paint);
        }
        else
        {
            executionContext.Canvas.SaveLayer(skBounds, null);
        }

        executionContext.Frames.OnSaveLayer();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }

    /// <inheritdoc />
    public override string ToString()
        => $"{nameof(SaveLayerCommand)} {((Paint != null) ? CommandHelpers.FormatPaint(Paint) : "no paint")}";
}
