using PdfPixel.Color.Paint;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws text at the origin using the given font and paint.
/// </summary>
public sealed class DrawTextCommand : PdfCommand, IMatrixCommand
{
    private readonly string _text;
    private readonly SKFont _baseFont;
    private readonly SKPaint _basePaint;

    /// <summary>
    /// Initializes the command with the given text, matrix, font and paint.
    /// </summary>
    public DrawTextCommand(string text, SKMatrix matrix, SKFont font, SKPaint basePaint)
    {
        _text = text;
        Matrix = matrix;
        _baseFont = font;
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public SKMatrix Matrix { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = _basePaint.Clone();
        bool antialias = executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        PdfPaintFactory.ApplyAntialias(_baseFont, antialias);
        CommandHelpers.ApplyModifiers(paint, executionContext);

        SKCanvas canvas = executionContext.Canvas;
        canvas.Save();
        canvas.Concat(Matrix);
        canvas.DrawText(_text, 0f, 0f, SKTextAlign.Left, _baseFont, paint);
        canvas.Restore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _baseFont.Dispose();
        _basePaint.Dispose();
    }
}
