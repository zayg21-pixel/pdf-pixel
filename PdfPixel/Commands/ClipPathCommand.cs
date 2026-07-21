using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a clipping path to the canvas.
/// </summary>
public sealed class ClipPathCommand : PdfCommand, IPathCommand
{
    /// <summary>
    /// Initializes the command with the given path and clip operation.
    /// </summary>
    public ClipPathCommand(PdfPath path, PdfClipOperation operation)
    {
        Path = path;
        Operation = operation.ToSkClipOperation();
    }

    /// <inheritdoc />
    public PdfPath Path { get; }

    /// <summary>
    /// Gets the clip operation applied to the canvas.
    /// </summary>
    public SKClipOperation Operation { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPath path = Path.ToSkPath();
        bool antialias = CommandHelpers.GetPathIsAntialias(path, executionContext);
        executionContext.Canvas.ClipPath(path, Operation, antialias);
        executionContext.Frames.OnClipPath(path, Operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
