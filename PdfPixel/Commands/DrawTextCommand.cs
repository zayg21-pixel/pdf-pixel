using PdfPixel.Color.Paint;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws text at the origin using the given font and paint.
/// </summary>
public sealed class DrawTextCommand : PdfCommand
{
    private readonly string _text;
    private readonly SKFont _baseFont;
    private readonly SKPaint _basePaint;

    /// <summary>
    /// Initializes the command with the given text, font and paint.
    /// </summary>
    public DrawTextCommand(string text, SKFont font, SKPaint basePaint)
    {
        _text = text;
        _baseFont = font;
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = _basePaint.Clone();
        bool antialias = executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        PdfPaintFactory.ApplyAntialias(_baseFont, antialias);
        CommandHelpers.ApplyModifiers(paint, executionContext);

        executionContext.Canvas.DrawText(_text, 0f, 0f, SKTextAlign.Left, _baseFont, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _baseFont.Dispose();
        _basePaint.Dispose();
    }
}
