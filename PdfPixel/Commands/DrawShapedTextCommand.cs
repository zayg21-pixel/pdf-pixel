using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Rendering.Text;
using PdfPixel.Text;
using SkiaSharp;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Draws shaped text at the origin by building a text blob from pre-shaped glyphs.
/// </summary>
public sealed class DrawShapedTextCommand : PdfCommand, IMatrixCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the given matrix, shaped glyphs, font and paint.
    /// </summary>
    public DrawShapedTextCommand(in PdfMatrix matrix, ShapedGlyph[] shapingResult, SKFont font, PdfPaint paint)
    {
        Matrix = matrix;
        ShapingResult = shapingResult;
        Font = font;
        Paint = paint;
    }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the pre-shaped glyphs drawn by this command.
    /// </summary>
    public ShapedGlyph[] ShapingResult { get; }

    /// <summary>
    /// Gets the font used to draw the shaped glyphs.
    /// </summary>
    public SKFont Font { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = Paint.ToSkiaPaint();
        bool antialias = executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        CommandHelpers.ApplyAntialias(Font, antialias);

        CommandHelpers.ApplyModifiers(paint, executionContext);

        using SKTextBlob? blob = TextRenderUtilities.BuildTextBlob(ShapingResult, Font);

        if (blob != null)
        {
            SKCanvas canvas = executionContext.Canvas;
            canvas.Save();
            canvas.Concat(Matrix.ToSkMatrix());
            canvas.DrawText(blob, 0f, 0f, paint);
            canvas.Restore();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => Font.Dispose();

    /// <inheritdoc />
    public override string ToString()
    {
        string chars = string.Join(" ", ShapingResult.Select(glyph => $"{glyph.CharacterInfo.Unicode}/{glyph.GlyphId}"));
        return $"{nameof(DrawShapedTextCommand)} {CommandHelpers.FormatMatrix(Matrix)} {CommandHelpers.FormatPaint(Paint)} \"{chars}\"";
    }
}
