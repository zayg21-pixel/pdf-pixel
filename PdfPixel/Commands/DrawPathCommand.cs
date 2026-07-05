using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a path using a paint, applying the command modifier to the paint before drawing.
/// </summary>
public sealed class DrawPathCommand : PdfCommand
{
    private readonly SKPath _path;
    private readonly SKPaint _basePaint;

    /// <summary>
    /// Initializes the command with the given path and paint.
    /// </summary>
    public DrawPathCommand(SKPath path, SKPaint basePaint)
    {
        _path = path;
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = _basePaint.Clone();
        CommandHelpers.ApplyModifiers(paint, executionContext);
        paint.IsAntialias = CommandHelpers.GetPathIsAntialias(_path, executionContext, paint);
        executionContext.Canvas.DrawPath(_path, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _path.Dispose();
        _basePaint.Dispose();
    }
}
