using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Fills the entire canvas with a paint.
/// Takes ownership of the paint.
/// </summary>
public sealed class DrawPaintCommand : PdfCommand
{
    private readonly SKPaint _basePaint;

    public DrawPaintCommand(SKPaint basePaint)
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
        canvas.DrawPaint(paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _basePaint.Dispose();
    }
}
