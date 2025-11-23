using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Rendering.State;
using System.Collections.Generic;

namespace PdfReader.Text;

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

        var array = arrayOperand.AsArray();
        if (array != null)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var item = array.GetValue(i);
                if (item.Type == PdfValueType.String)
                {
                    var pdfText = PdfText.FromOperand(item);
                    AddShapedGlyphsForText(pdfText, font, state, buffer);
                }
                else
                {
                    // Positioning adjustment (negative = move left, positive = move right)
                    var adjustment = item.AsFloat();
                    var adjustmentInUserSpace = -adjustment / 1000f;
                    if (buffer.Count > 0)
                    {
                        var last = buffer[buffer.Count - 1];
                        buffer[buffer.Count - 1] = new ShapedGlyph(last.GlyphId, last.Unicode, last.Width, last.AdvanceAfter + adjustmentInUserSpace);
                    }
                    else
                    {
                        buffer.Add(new ShapedGlyph(0,  null, 0, adjustmentInUserSpace));
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
        AddShapedGlyphsForText(pdfText, font, state, buffer);
    }

    /// <summary>
    /// Shapes a PdfText and appends the resulting glyphs to the output list.
    /// </summary>
    private static void AddShapedGlyphsForText(PdfText pdfText, PdfFontBase font, PdfGraphicsState state, List<ShapedGlyph> output)
    {
        var codes = font.ExtractCharacterCodes(pdfText.RawBytes);
        for (int codeIndex = 0; codeIndex < codes.Length; codeIndex++)
        {
            var info = font.ExtractCharacterInfo(codes[codeIndex]);
            string unicode = info.Unicode;
            bool isSpace = unicode == " ";
            float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);
            output.Add(new ShapedGlyph(info.Gid, info.Unicode, info.Width, spacing / state.FontSize));
        }
    }
}
