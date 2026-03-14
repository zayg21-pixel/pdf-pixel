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
    private readonly bool _antialias;

    public ClipPathCommand(SKPath path, SKClipOperation operation, bool antialias)
    {
        _path = new SKPath(path);
        _operation = operation;
        _antialias = antialias;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers)
    {
        canvas.ClipPath(_path, _operation, _antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _path.Dispose();
    }
}
