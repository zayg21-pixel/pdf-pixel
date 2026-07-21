using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Clips using the fill outline of a path stroked with a given paint. Isolates the Skia-specific
/// stroke-to-fill conversion (<see cref="SKPaint.GetFillPath(SKPath)"/>) behind the command boundary.
/// </summary>
public sealed class ClipStrokePathCommand : PdfCommand
{
    private readonly PdfPath _sourcePath;
    private readonly PdfPaint _strokePaint;

    /// <summary>
    /// Initializes the command with the given source path, stroke paint, and clip operation.
    /// </summary>
    public ClipStrokePathCommand(PdfPath sourcePath, PdfPaint strokePaint, PdfClipOperation operation)
    {
        _sourcePath = sourcePath;
        _strokePaint = strokePaint;
        Operation = operation.ToSkClipOperation();
    }

    /// <summary>
    /// Gets the clip operation applied to the canvas.
    /// </summary>
    public SKClipOperation Operation { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPath path = BuildStrokeOutline(_sourcePath, _strokePaint);
        bool antialias = CommandHelpers.GetPathIsAntialias(path, executionContext);
        executionContext.Canvas.ClipPath(path, Operation, antialias);
        executionContext.Frames.OnClipPath(path, Operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }

    private static SKPath BuildStrokeOutline(PdfPath sourcePath, PdfPaint strokePaint)
    {
        using SKPath skSourcePath = sourcePath.ToSkPath();
        using SKPaint skStrokePaint = strokePaint.ToSkiaPaint();

        return skStrokePaint.GetFillPath(skSourcePath) ?? new SKPath(skSourcePath);
    }
}
