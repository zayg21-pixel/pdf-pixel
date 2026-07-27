using PdfPixel.Color.Paint;
using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using SkiaSharp;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Draws text at the origin using the given typeface, size and paint. Resolves the backing
/// <see cref="SKTypeface"/> lazily at execution time via <see cref="PdfCommandExecutionContext.Cache"/>,
/// so the same native typeface is reused across every command that draws with it during a replay.
/// </summary>
public sealed class DrawTextCommand : PdfCommand, IMatrixCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the given text, matrix, typeface, font size and paint.
    /// </summary>
    public DrawTextCommand(string text, in PdfMatrix matrix, IPdfTypeface typeface, float fontSize, PdfPaint paint)
    {
        Text = text;
        Matrix = matrix;
        Typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));
        FontSize = fontSize;
        Paint = paint;
    }

    /// <summary>
    /// Gets the text drawn by this command.
    /// </summary>
    public string Text { get; }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the typeface used to draw the text.
    /// </summary>
    public IPdfTypeface Typeface { get; }

    /// <summary>
    /// Gets the font size (em height) used to draw the text.
    /// </summary>
    public float FontSize { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        using SKPaint paint = Paint.ToSkiaPaint();
        bool antialias = executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        CommandHelpers.ApplyModifiers(paint, executionContext);

        SKTypeface skTypeface = CommandHelpers.GetOrCreateSkTypeface(executionContext, Typeface);
        using SKFont font = new(skTypeface, FontSize);
        CommandHelpers.ApplyAntialias(font, antialias);

        SKCanvas canvas = executionContext.Canvas;
        canvas.Save();
        canvas.Concat(Matrix.ToSkMatrix());
        canvas.DrawText(Text, 0f, 0f, SKTextAlign.Left, font, paint);
        canvas.Restore();
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawTextCommand)} {CommandHelpers.FormatMatrix(Matrix)} {CommandHelpers.FormatPaint(Paint)} \"{Text}\"";
}
