using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;
using System.IO;

namespace PdfPixel.Commands;

/// <summary>
/// Clips using the fill outline of a path stroked with a given paint. Isolates the Skia-specific
/// stroke-to-fill conversion (<see cref="SKPaint.GetFillPath(SKPath)"/>) behind the command boundary.
/// </summary>
public sealed class ClipStrokePathCommand : PdfCommand, IPathCommand
{
    /// <summary>
    /// Initializes the command with the given source path, stroke paint, and clip operation.
    /// The source path is converted to its stroked fill outline immediately.
    /// </summary>
    public ClipStrokePathCommand(PdfPath sourcePath, PdfPaint strokePaint, PdfClipOperation operation)
    {
        Path = BuildStrokeOutline(sourcePath, strokePaint);
        Operation = operation.ToSkClipOperation();
    }

    /// <inheritdoc />
    public SKPath Path { get; }

    /// <summary>
    /// Gets the clip operation applied to the canvas.
    /// </summary>
    public SKClipOperation Operation { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        bool antialias = CommandHelpers.GetPathIsAntialias(Path, executionContext);
        executionContext.Canvas.ClipPath(Path, Operation, antialias);
        executionContext.Frames.OnClipPath(Path, Operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => Path.Dispose();

    private static SKPath BuildStrokeOutline(PdfPath sourcePath, PdfPaint strokePaint)
    {
        using SKPath skSourcePath = sourcePath.ToSkPath();
        using SKPaint skStrokePaint = strokePaint.ToSkiaPaint();

        return skStrokePaint.GetFillPath(skSourcePath) ?? new SKPath(skSourcePath);
    }
}
