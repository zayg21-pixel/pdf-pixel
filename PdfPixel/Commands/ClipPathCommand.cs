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
        Path = path.ToSkPath();
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
}
