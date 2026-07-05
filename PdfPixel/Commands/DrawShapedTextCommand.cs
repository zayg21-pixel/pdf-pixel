using PdfPixel.Color.Paint;
using PdfPixel.Rendering.Text;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws shaped text at the origin by building a text blob from pre-shaped glyphs.
/// </summary>
public sealed class DrawShapedTextCommand : PdfCommand
{
    private readonly ShapedGlyph[] _shapingResult;
    private readonly SKFont _baseFont;
    private readonly SKPaint _basePaint;

    /// <summary>
    /// Initializes the command with the given shaped glyphs, font and paint.
    /// </summary>
    public DrawShapedTextCommand(ShapedGlyph[] shapingResult, SKFont font, SKPaint basePaint)
    {
        _shapingResult = shapingResult;
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

        using SKTextBlob? blob = TextRenderUtilities.BuildTextBlob(_shapingResult, _baseFont);

        if (blob != null)
        {
            executionContext.Canvas.DrawText(blob, 0f, 0f, paint);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _baseFont?.Dispose();
        _basePaint.Dispose();
    }
}
