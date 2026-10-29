using PdfPixel.Color;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Fonts.Substitution;
using PdfPixel.Geometry;
using PdfPixel.Rendering.Text;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Utility class for drawing text on annotations without a default appearance.
/// </summary>
internal static class PdfAnnotationTextRenderer
{
    private const float LineCaptionFontSize = 12f;
    private const float LineCaptionMargin = LineCaptionFontSize / 4f;

    private static readonly PdfSubstitutionInfo LineCaptionFont = new(
        PdfStandardFontName.Helvetica,
        PdfSubstitutionInfo.NormalWeight,
        PdfSubstitutionInfo.NormalWidth,
        italicAngle: 0f);

    /// <summary>
    /// Shapes <paramref name="text"/> as a single line in <paramref name="typeface"/>, in em-relative units.
    /// </summary>
    /// <param name="text">The text to shape.</param>
    /// <param name="typeface">The typeface the text is drawn in.</param>
    /// <returns>The shaped glyphs.</returns>
    public static ShapedGlyph[] Shape(string text, IPdfTypeface typeface)
    {
        ShapedGlyphBuffer buffer = new();
        ShapedGlyphBuilder.BuildFromText(text, typeface, buffer);

        return buffer.Glyphs.ToArray();
    }

    /// <summary>
    /// Draws <paramref name="glyphs"/> with their baseline starting at <paramref name="origin"/>, in the coordinate
    /// space <paramref name="matrix"/> maps to the page.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="glyphs">The glyphs shaped by <see cref="Shape"/>.</param>
    /// <param name="matrix">The matrix from the text's coordinate space to the page.</param>
    /// <param name="origin">The start of the baseline.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="color">The text color.</param>
    public static void DrawText(
        IPdfCommandProcessor processor,
        ShapedGlyph[] glyphs,
        in PdfMatrix matrix,
        in PdfPoint origin,
        float fontSize,
        in PdfColor color)
    {
        PdfMatrix textMatrix = PdfMatrix.Concat(matrix, PdfMatrix.CreateScaleTranslation(fontSize, -fontSize, origin.X, origin.Y));

        processor.Process(new DrawShapedTextCommand(textMatrix, glyphs, PdfAnnotationPaintFactory.CreateFillPaint(color)));
    }

    /// <summary>
    /// Lays out <paramref name="text"/> as the caption of the line from <paramref name="lineStart"/> to
    /// <paramref name="lineEnd"/>, in Helvetica, along the line from its midpoint. An inline caption that does not fit
    /// the line goes on top of it. The top of the line is the side its leader lines extend to, and a caption on top
    /// keeps clear of the leader line extensions.
    /// </summary>
    /// <param name="document">The document whose fonts the caption is shaped with.</param>
    /// <param name="text">The caption text.</param>
    /// <param name="lineStart">The start point of the line.</param>
    /// <param name="lineEnd">The end point of the line.</param>
    /// <param name="position">Whether the caption is centered inside the line or on top of it.</param>
    /// <param name="offset">The offset of the caption along the line, then perpendicular to it.</param>
    /// <param name="lineWidth">The width of the line.</param>
    /// <param name="leaderLength">The leader line length; negative when the leader lines extend to the right of the line.</param>
    /// <param name="leaderExtension">The length the leader lines extend past the line.</param>
    /// <returns>The caption layout, or null when <paramref name="text"/> is empty.</returns>
    public static PdfLineCaptionLayout? LayoutLineCaption(
        IPdfDocumentInternal document,
        string text,
        in PdfPoint lineStart,
        in PdfPoint lineEnd,
        PdfLineCaptionPosition position,
        in PdfPoint offset,
        float lineWidth,
        float leaderLength,
        float leaderExtension)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        SfntPdfTypeface typeface = document.FontProvider.GetTypefaceByUnicode(LineCaptionFont, text);
        ShapedGlyph[] glyphs = Shape(text, typeface);
        float width = TextRenderUtilities.GetTextWidth(glyphs) * LineCaptionFontSize;
        float ascent = typeface.Metrics.Ascent * LineCaptionFontSize;
        float descent = typeface.Metrics.Descent * LineCaptionFontSize;
        bool isInline = position == PdfLineCaptionPosition.Inline
            && width + LineCaptionFontSize <= PdfAnnotationGeometry.GetDistance(lineStart, lineEnd);
        bool isTopBelowLine = leaderLength < 0f;
        float topClearance = (lineWidth / 2f) + leaderExtension;

        float baseline;
        if (isInline)
        {
            baseline = -(ascent + descent) / 2f;
        }
        else if (isTopBelowLine)
        {
            baseline = -topClearance - ascent;
        }
        else
        {
            baseline = topClearance - descent;
        }

        PdfPoint origin = new(offset.X - (width / 2f), offset.Y + baseline);
        PdfRectangle area = new(origin.X - LineCaptionMargin, origin.Y + descent, origin.X + width + LineCaptionMargin, origin.Y + ascent);

        return new PdfLineCaptionLayout(glyphs, PdfAnnotationGeometry.CreateLineMatrix(lineStart, lineEnd), origin, area);
    }

    /// <summary>
    /// Draws the caption laid out by <see cref="LayoutLineCaption"/> in black.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="layout">The caption layout.</param>
    public static void DrawLineCaption(IPdfCommandProcessor processor, in PdfLineCaptionLayout layout)
        => DrawText(processor, layout.Glyphs, layout.LineMatrix, layout.Origin, LineCaptionFontSize, PdfColors.Black);
}
