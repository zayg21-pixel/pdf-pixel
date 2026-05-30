using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a clipping path to the canvas.
/// Clones the path on construction to ensure immutability.
/// </summary>
public sealed class ClipPathCommand : PdfCommand
{
    private readonly SKPath _path;
    private readonly SKClipOperation _operation;

    /// <summary>
    /// Initializes the command with a cloned copy of the given path and clip operation.
    /// </summary>
    public ClipPathCommand(SKPath path, SKClipOperation operation)
    {
        _path = new SKPath(path);
        _operation = operation;
    }

    /// <summary>
    /// Initializes the command with a rectangular clip path and the given clip operation.
    /// </summary>
    public ClipPathCommand(SKRect rect, SKClipOperation operation)
    {
        _path = new SKPath();
        _path.AddRect(rect);
        _operation = operation;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        bool antialias = CommandHelpers.GetPathIsAntialias(_path, canvas, executionContext);
        canvas.ClipPath(_path, _operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _path.Dispose();
}
