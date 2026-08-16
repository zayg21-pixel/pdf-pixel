using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
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
        Paint = paint;
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
    public override PdfCommandKind Kind => PdfCommandKind.DrawShapedText;

    /// <inheritdoc />
    public override string ToString()
    {
        string chars = string.Join(" ", ShapingResult.Span.ToArray().Select(glyph => $"{glyph.CharacterInfo.Unicode}/{glyph.GlyphId}"));
        return $"{nameof(DrawShapedTextCommand)} {CommandHelpers.FormatMatrix(Matrix)} {CommandHelpers.FormatPaint(Paint)} \"{chars}\"";
    }
}
