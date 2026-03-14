using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;
// TODO: [MEDIUM] remove, replace with Path render command
/// <summary>
/// Draws a rectangle that fills the current local clip bounds.
/// Takes ownership of the paint.
/// </summary>
public sealed class DrawRectCommand : PdfCommand
{
    private readonly SKPaint _basePaint;

    public DrawRectCommand(SKPaint basePaint)
    {
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
        canvas.DrawRect(canvas.LocalClipBounds, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _basePaint.Dispose();
    }
}
