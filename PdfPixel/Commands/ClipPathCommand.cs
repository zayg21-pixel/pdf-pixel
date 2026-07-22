using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a clipping path to the canvas. When <see cref="Paint"/> is a stroke paint, the path is
/// first converted to its stroke fill outline (<see cref="SKPaint.GetFillPath(SKPath)"/>); otherwise
/// the path is used as-is.
/// </summary>
public sealed class ClipPathCommand : PdfCommand, IPathCommand
{
    /// <summary>
    /// Initializes the command with the given path, clip operation, and optional stroke/fill paint.
    /// </summary>
    public ClipPathCommand(PdfPath path, PdfClipOperation operation, PdfPaint? paint = null)
    {
        Path = path;
        Paint = paint;
        Operation = operation;
    }

    /// <inheritdoc />
    public PdfPath Path { get; }

    /// <summary>
    /// Gets the paint that determines whether <see cref="Path"/> is clipped as a stroke outline or as-is.
    /// Null clips the path as-is.
    /// </summary>
    public PdfPaint? Paint { get; }

    /// <summary>
    /// Gets the clip operation applied to the canvas.
    /// </summary>
    public PdfClipOperation Operation { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPath sourcePath = Path.ToSkPath();
        using SKPaint? strokePaint = BuildStrokePaint(executionContext);
        bool antialias = CommandHelpers.GetPathIsAntialias(sourcePath, executionContext, strokePaint);
        using SKPath clipPath = BuildClipPath(sourcePath, strokePaint);
        SKClipOperation skOperation = Operation.ToSkClipOperation();

        executionContext.Canvas.ClipPath(clipPath, skOperation, antialias);

        if (strokePaint != null && Paint != null)
        {
            executionContext.Frames.OnClipStrokePath(Path, Operation, Paint);
        }
        else
        {
            executionContext.Frames.OnClipPath(Path, Operation);
        }
    }

    private SKPaint? BuildStrokePaint(PdfCommandExecutionContext executionContext)
    {
        if (Paint == null || Paint.Style != PdfPaintStyle.Stroke)
        {
            return null;
        }

        SKPaint strokePaint = Paint.ToSkiaPaint();
        strokePaint.StrokeWidth = CommandHelpers.GetMinimumStrokeWidth(executionContext, Paint);
        return strokePaint;
    }

    private static SKPath BuildClipPath(SKPath sourcePath, SKPaint? strokePaint)
    {
        if (strokePaint == null)
        {
            return new SKPath(sourcePath);
        }

        return strokePaint.GetFillPath(sourcePath) ?? new SKPath(sourcePath);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
