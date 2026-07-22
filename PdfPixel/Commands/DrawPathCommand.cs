using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a path using a paint, applying the command modifier to the paint before drawing.
/// </summary>
public sealed class DrawPathCommand : PdfCommand, IPathCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the given path and paint.
    /// </summary>
    public DrawPathCommand(PdfPath path, PdfPaint paint)
    {
        Path = path;
        Paint = paint;
    }

    /// <inheritdoc />
    public PdfPath Path { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPath path = Path.ToSkPath();
        using SKPaint paint = Paint.ToSkiaPaint();
        CommandHelpers.ApplyModifiers(paint, executionContext);
        paint.IsAntialias = CommandHelpers.GetPathIsAntialias(path, executionContext, paint);
        paint.StrokeWidth = CommandHelpers.GetMinimumStrokeWidth(executionContext, Paint);

        executionContext.Canvas.DrawPath(path, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawPathCommand)} {CommandHelpers.FormatPaint(Paint)}";
}
