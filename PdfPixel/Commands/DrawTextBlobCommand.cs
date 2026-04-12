using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a text blob at the origin using a paint, applying the command modifier before drawing.
/// Takes ownership of both the blob and the paint. Both are disposed with the command.
/// </summary>
public sealed class DrawTextBlobCommand : PdfCommand
{
    private readonly SKTextBlob _blob;
    private readonly SKPaint _basePaint;

    public DrawTextBlobCommand(SKTextBlob blob, SKPaint basePaint)
    {
        _blob = blob;
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        using var paint = _basePaint.Clone();
        paint.IsAntialias = executionContext.RenderingParameters.Antialias;
        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(paint);
        }
        canvas.DrawText(_blob, 0f, 0f, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _blob.Dispose();
        _basePaint.Dispose();
    }
}
