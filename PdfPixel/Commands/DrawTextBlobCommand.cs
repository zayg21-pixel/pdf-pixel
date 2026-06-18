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

    /// <summary>
    /// Initializes the command taking ownership of both the text blob and the paint.
    /// </summary>
    public DrawTextBlobCommand(SKTextBlob blob, SKPaint basePaint)
    {
        _blob = blob;
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = _basePaint.Clone();
        paint.IsAntialias = executionContext.Parameters.Antialias;
        CommandHelpers.ApplyModifiers(paint, modifiers);

        executionContext.Canvas.DrawText(_blob, 0f, 0f, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _blob?.Dispose();
        _basePaint.Dispose();
    }
}
