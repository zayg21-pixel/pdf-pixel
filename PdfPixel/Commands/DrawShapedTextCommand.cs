using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Rendering.Text;
using PdfPixel.Text;
using System;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Draws shaped text at the origin by building one or more text blobs from pre-shaped glyphs.
/// </summary>
public sealed class DrawShapedTextCommand : PdfCommand, IMatrixCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the given matrix, shaped glyphs and paint.
    /// </summary>
    public DrawShapedTextCommand(in PdfMatrix matrix, in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfPaint paint)
    {
        Matrix = matrix;
        ShapingResult = shapingResult;
        Paint = paint ?? throw new ArgumentNullException(nameof(paint));

        PdfRectangle? textBounds = PdfTextRenderUtilities.GetTextBounds(shapingResult.Span, matrix);

        if (textBounds != null && paint.Style == PdfPaintStyle.Stroke)
        {
            Bounds = textBounds.Value.InflateForStroke(paint);
        }
        else
        {
            Bounds = textBounds;
        }
    }

    /// <summary>
    /// Initializes the command with the given matrix, shaped glyphs, paint and group source.
    /// </summary>
    public DrawShapedTextCommand(in PdfMatrix matrix, in ReadOnlyMemory<ShapedGlyph> shapingResult, PdfPaint paint, PdfGroupSource source)
        : this(matrix, shapingResult, paint)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the pre-shaped glyphs drawn by this command.
    /// </summary>
    public ReadOnlyMemory<ShapedGlyph> ShapingResult { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public PdfRectangle? Bounds { get; }

    /// <summary>
    /// Gets the group the text is painted with, or <see langword="null"/> when it is painted in the paint's color.
    /// </summary>
    public PdfGroupSource? Source { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawShapedText;

    /// <inheritdoc />
    public override string ToString()
    {
        string chars = string.Join(" ", ShapingResult.Span.ToArray().Select(glyph => $"{glyph.CharacterInfo.Unicode}/{glyph.GlyphId}"));
        return $"{nameof(DrawShapedTextCommand)} {PdfCommandFormatting.FormatMatrix(Matrix)} {PdfCommandFormatting.FormatPaint(Paint)}{((Source != null) ? " " + Source : string.Empty)} \"{chars}\"";
    }
}
