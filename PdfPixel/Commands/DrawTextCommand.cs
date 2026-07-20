using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Draws text at the origin using the given font and paint.
/// </summary>
public sealed class DrawTextCommand : PdfCommand, IMatrixCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the given text, matrix, font and paint.
    /// </summary>
    public DrawTextCommand(string text, in PdfMatrix matrix, SKFont font, SKPaint basePaint)
    {
        Text = text;
        Matrix = matrix;
        Font = font;
        Paint = basePaint;
    }

    /// <summary>
    /// Initializes the command with the given text, matrix, font and paint.
    /// </summary>
    public DrawTextCommand(string text, in PdfMatrix matrix, SKFont font, PdfPaint basePaint)
        : this(text, matrix, font, basePaint.ToSkiaPaint())
    {
    }

    /// <summary>
    /// Gets the text drawn by this command.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the font used to draw the text.
    /// </summary>
    public SKFont Font { get; }

    /// <inheritdoc />
    public SKPaint Paint { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = Paint.Clone();
        bool antialias = executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        PdfPaintFactory.ApplyAntialias(Font, antialias);
        CommandHelpers.ApplyModifiers(paint, executionContext);

        SKCanvas canvas = executionContext.Canvas;
        canvas.Save();
        canvas.Concat(Matrix.ToSkMatrix());
        canvas.DrawText(Text, 0f, 0f, SKTextAlign.Left, Font, paint);
        canvas.Restore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        Font.Dispose();
        Paint.Dispose();
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawTextCommand)} {CommandHelpers.FormatMatrix(Matrix)} {CommandHelpers.FormatPaint(Paint)} \"{Text}\"";
}
