using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a path using a paint, applying the command modifier to the paint before drawing.
/// Clones the path (mutable) and takes ownership of the paint. Both are disposed with the command.
/// </summary>
public sealed class DrawPathCommand : PdfCommand
{
    private readonly SKPath _path;
    private readonly SKPaint _basePaint;

    public DrawPathCommand(SKPath path, SKPaint basePaint)
    {
        _path = new SKPath(path);
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers)
    {
        using var paint = _basePaint.Clone();
        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(paint);
        }
        canvas.DrawPath(_path, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _path.Dispose();
        _basePaint.Dispose();
    }
}
