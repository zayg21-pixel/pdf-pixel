using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a bitmap at the canvas origin.
/// Takes ownership of the bitmap.
/// </summary>
public sealed class DrawBitmapCommand : PdfCommand
{
    private readonly SKBitmap _bitmap;

    public DrawBitmapCommand(SKBitmap bitmap)
    {
        _bitmap = bitmap;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers)
    {
        canvas.DrawBitmap(_bitmap, SKPoint.Empty);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _bitmap.Dispose();
    }
}
