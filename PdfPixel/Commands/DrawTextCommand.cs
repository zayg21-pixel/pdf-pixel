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
    public DrawTextCommand(string text, in PdfMatrix matrix, SKFont font, PdfPaint paint)
    {
        Text = text;
        Matrix = matrix;
        Font = font;
        Paint = paint;
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
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = Paint.ToSkiaPaint();
        bool antialias = executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        CommandHelpers.ApplyAntialias(Font, antialias);
        CommandHelpers.ApplyModifiers(paint, executionContext);

        SKCanvas canvas = executionContext.Canvas;
        canvas.Save();
        canvas.Concat(Matrix.ToSkMatrix());
        canvas.DrawText(Text, 0f, 0f, SKTextAlign.Left, Font, paint);
        canvas.Restore();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => Font.Dispose();

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawTextCommand)} {CommandHelpers.FormatMatrix(Matrix)} {CommandHelpers.FormatPaint(Paint)} \"{Text}\"";
}
