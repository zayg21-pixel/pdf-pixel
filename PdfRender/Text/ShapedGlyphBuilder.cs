using PdfRender.Fonts.Model;
using PdfRender.Models;
using PdfRender.Rendering.State;
using System.Collections.Generic;

namespace PdfRender.Text;

/// <summary>
/// Utility to convert PDF text operands (string or TJ array) directly into a list of shaped glyphs.
/// Bypasses PdfTextSequence for direct rendering.
/// </summary>
public static class ShapedGlyphBuilder
{
    /// <summary>
    /// Converts a PDF TJ array operand into a list of shaped glyphs for rendering.
    /// The caller must provide a non-null <paramref name="buffer"/>, which will be cleared and filled.
    /// If any argument is invalid, the method returns silently and does nothing.
    /// </summary>
    public static void BuildFromArray(
        IPdfValue arrayOperand,
        PdfGraphicsState state,
        List<ShapedGlyph> buffer)
    {
        // Guard: do nothing if arguments are invalid
        if (arrayOperand == null || arrayOperand.Type != PdfValueType.Array || buffer == null)
        {
            return;
        }
        buffer.Clear();

        var font = state?.CurrentFont;
        if (font == null)
        {
            return;
        }

        bool isVertical = font.WritingMode == Fonts.Mapping.CMapWMode.Vertical;

        var array = arrayOperand.AsArray();
        float x = 0f;
        float y = 0f;

        if (array != null)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var item = array.GetValue(i);
                if (item.Type == PdfValueType.String)
                {
                    var pdfText = PdfText.FromOperand(item);
                    AddShapedGlyphsForText(pdfText, font, state, buffer, ref x, ref y);
                }
                else
                {
                    // Positioning adjustment (negative = move left, positive = move right)
                    var adjustment = item.AsFloat();
                    var adjustmentInUserSpace = -adjustment / 1000f;

                    if (isVertical)
                    {
                        y += -adjustmentInUserSpace;
                    }
                    else
                    {
                        x += adjustmentInUserSpace;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts a PDF string operand into a list of shaped glyphs for rendering.
    /// The caller must provide a non-null <paramref name="buffer"/>, which will be cleared and filled.
    /// If any argument is invalid, the method returns silently and does nothing.
    /// </summary>
    public static void BuildFromString(
        IPdfValue stringOperand,
        PdfGraphicsState state,
        List<ShapedGlyph> buffer)
    {
        // Guard: do nothing if arguments are invalid
        if (stringOperand == null || stringOperand.Type != PdfValueType.String || buffer == null)
        {
            return;
        }

        buffer.Clear();

        var font = state?.CurrentFont;
        if (font == null)
        {
            return;
        }

        var pdfText = PdfText.FromOperand(stringOperand);
        float x = 0f;
        float y = 0f;

        AddShapedGlyphsForText(pdfText, font, state, buffer, ref x, ref y);
    }

    /// <summary>
    /// Shapes a PdfText and appends the resulting glyphs to the output list.
    /// </summary>
    private static void AddShapedGlyphsForText(PdfText pdfText, PdfFontBase font, PdfGraphicsState state, List<ShapedGlyph> output, ref float x, ref float y)
    {
        var codes = font.ExtractCharacterCodes(pdfText.RawBytes);
        bool isVertical = font.WritingMode == Fonts.Mapping.CMapWMode.Vertical;

        for (int codeIndex = 0; codeIndex < codes.Length; codeIndex++)
        {
            var info = font.ExtractCharacterInfo(codes[codeIndex]);
            string unicode = info.Unicode;
            bool isSpace = unicode == " ";
            float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);
            float advance = spacing / state.FontSize;

            float xCursor = x + info.Offset.X;
            float rightEdge = xCursor + info.OriginalWidth;

            for (int i = 0; i < info.Gid.Length; i++)
            {
                int? id = info.Gid.Length > 1 ? i : null;
                uint gid = info.Gid[i];
                float width = info.Width[i];
                float glyphRightAdvance = rightEdge - xCursor;
                float advacementToEnd = isVertical ? -info.Advancement : glyphRightAdvance;
                output.Add(new ShapedGlyph(info, id, gid, advacementToEnd, info.XScale, xCursor, y + info.Offset.Y));
                xCursor += width;
            }

            if (isVertical)
            {
                y -= info.Advancement + advance;
            }
            else
            {
                x += info.Advancement + advance;
            }
        }
    }
}
