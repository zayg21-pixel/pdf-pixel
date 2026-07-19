using PdfPixel.Path;
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
    public DrawPathCommand(SKPath path, SKPaint basePaint)
    {
        Path = path;
        Paint = basePaint;
    }

    /// <summary>
    /// Initializes the command with the given path and paint.
    /// </summary>
    public DrawPathCommand(PdfPath path, SKPaint basePaint)
        : this(path.ToSkPath(), basePaint)
    {
    }

    /// <inheritdoc />
    public SKPath Path { get; }

    /// <inheritdoc />
    public SKPaint Paint { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => PdfCommandFeatures.Scale;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = Paint.Clone();
        CommandHelpers.ApplyModifiers(paint, executionContext);
        paint.IsAntialias = CommandHelpers.GetPathIsAntialias(Path, executionContext, paint);

        if ((paint.Style == SKPaintStyle.Stroke || paint.Style == SKPaintStyle.StrokeAndFill) && paint.StrokeWidth > 0)
        {
            float minimumStrokeWidth = CommandHelpers.GetMinimumStrokeWidth(executionContext);
            if (paint.StrokeWidth < minimumStrokeWidth)
            {
                paint.StrokeWidth = minimumStrokeWidth;
            }
        }

        executionContext.Canvas.DrawPath(Path, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        Path.Dispose();
        Paint.Dispose();
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawPathCommand)} {CommandHelpers.FormatPaint(Paint)}";
}
