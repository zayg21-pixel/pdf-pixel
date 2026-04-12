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

    public ClipPathCommand(SKPath path, SKClipOperation operation)
    {
        _path = new SKPath(path);
        _operation = operation;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        canvas.ClipPath(_path, _operation, executionContext.RenderingParameters.Antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _path.Dispose();
    }
}
