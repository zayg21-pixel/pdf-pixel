using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a clipping path to the canvas.
/// </summary>
public sealed class ClipPathCommand : PdfCommand
{
    private readonly SKPath _path;
    private readonly SKClipOperation _operation;

    /// <summary>
    /// Initializes the command with the given path and clip operation.
    /// The command takes ownership of the path.
    /// </summary>
    public ClipPathCommand(SKPath path, SKClipOperation operation)
    {
        _path = path;
        _operation = operation;
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        bool antialias = CommandHelpers.GetPathIsAntialias(_path, executionContext);
        executionContext.Canvas.ClipPath(_path, _operation, antialias);
        executionContext.Frames.OnClipPath(_path, _operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => _path.Dispose();
}
