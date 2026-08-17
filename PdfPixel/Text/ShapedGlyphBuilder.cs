using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using System;
using System.Collections.Generic;

namespace PdfPixel.Text;

/// <summary>
/// Converts PDF text operands (string or TJ array) into a list of shaped glyphs.
/// </summary>
public static class ShapedGlyphBuilder
{
    /// <summary>
    /// Shapes Unicode text along a single horizontal baseline, in em-relative units (1.0 = one em).
    /// A codepoint the typeface has no glyph for gets a null glyph id and a zero advance.
    /// </summary>
    /// <param name="text">The Unicode text to shape.</param>
    /// <param name="typeface">The typeface resolving glyph ids and advances.</param>
    /// <param name="buffer">The list to clear and fill with the shaped glyphs.</param>
    public static void BuildFromText(string text, IPdfTypeface typeface, List<ShapedGlyph> buffer)
    {
        if (typeface == null)
        {
            throw new ArgumentNullException(nameof(typeface));
        }

        if (buffer == null)
        {
            throw new ArgumentNullException(nameof(buffer));
        }

        buffer.Clear();

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        float x = 0f;
        int index = 0;

        while (index < text.Length)
        {
            int codepointLength = (char.IsSurrogatePair(text, index)) ? 2 : 1;
            string unicode = text.Substring(index, codepointLength);
            index += codepointLength;

            ushort? gid = typeface.GetGid(unicode);
            float width = (gid == null) ? 0f : typeface.GetWidth(gid.Value) ?? 0f;

            PdfCharacterInfo info = new(
                (uint)char.ConvertToUtf32(unicode, 0),
                typeface,
                unicode,
                [gid],
                width,
                [width],
                xScale: 1f,
                offset: default,
                advancement: width);

            buffer.Add(new ShapedGlyph(info, groupId: null, gid, width, info.XScale, x, 0f));
            x += width;
        }
    }

    /// <summary>
    /// Shapes a PDF TJ array operand into <paramref name="buffer"/>, which is cleared first.
    /// </summary>
    public static void BuildFromArray(
        IPdfValue arrayOperand,
        PdfGraphicsState state,
        List<ShapedGlyph> buffer)
    {
        if (arrayOperand == null || arrayOperand.Type != PdfValueType.Array || state == null || buffer == null)
        {
            return;
        }

        buffer.Clear();

        PdfFontBase? font = state.CurrentFont;
        if (font == null)
        {
            return;
        }

        bool isVertical = font.WritingMode == Fonts.Mapping.CMapWMode.Vertical;

        PdfArray? array = arrayOperand.AsArray();
        float x = 0f;
        float y = 0f;

        if (array != null)
        {
            for (int i = 0; i < array.Count; i++)
            {
                IPdfValue? item = array.GetValue(i);

                if (item == null)
                {
                    continue;
                }

                if (item.Type == PdfValueType.String)
                {
                    PdfText pdfText = PdfText.FromOperand(item);
                    AddShapedGlyphsForText(pdfText, font, state, buffer, ref x, ref y);
                }
                else
                {
                    float? adjustment = item.AsFloat();
                    if (adjustment == null)
                    {
                        continue;
                    }

                    float adjustmentInUserSpace = -adjustment.Value / 1000f;

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
    /// Shapes a PDF string operand into <paramref name="buffer"/>, which is cleared first.
    /// </summary>
    public static void BuildFromString(
        IPdfValue stringOperand,
        PdfGraphicsState state,
        List<ShapedGlyph> buffer)
    {
        if (stringOperand == null || stringOperand.Type != PdfValueType.String || state == null || buffer == null)
        {
            return;
        }

        buffer.Clear();

        PdfFontBase? font = state.CurrentFont;
        if (font == null)
        {
            return;
        }

        PdfText pdfText = PdfText.FromOperand(stringOperand);
        float x = 0f;
        float y = 0f;

        AddShapedGlyphsForText(pdfText, font, state, buffer, ref x, ref y);
    }

    /// <summary>
    /// Shapes a PdfText and appends the resulting glyphs to the output list.
    /// </summary>
    private static void AddShapedGlyphsForText(in PdfText pdfText, PdfFontBase font, PdfGraphicsState state, List<ShapedGlyph> output, ref float x, ref float y)
    {
        Fonts.Mapping.PdfCharacterCode[] codes = font.ExtractCharacterCodes(pdfText.RawBytes);
        bool isVertical = font.WritingMode == Fonts.Mapping.CMapWMode.Vertical;

        for (int codeIndex = 0; codeIndex < codes.Length; codeIndex++)
        {
            PdfCharacterInfo info = font.ExtractCharacterInfo(codes[codeIndex]);

            // Word spacing applies to the single-byte character code 32, never to a byte 32 that is
            // part of a multi-byte code.
            ReadOnlySpan<byte> codeBytes = info.CharacterCode.Bytes.Span;
            bool isSpace = codeBytes.Length == 1 && codeBytes[0] == 0x20;
            float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);
            float advance = spacing / state.FontSize;

            float xCursor = x + info.Offset.X;
            float rightEdge = xCursor + info.OriginalWidth;

            for (int i = 0; i < info.Gid.Length; i++)
            {
                int? id = (info.Gid.Length > 1) ? i : null;
                ushort? gid = info.Gid[i];
                float width = info.Width[i];
                bool isLastGid = i == info.Gid.Length - 1;

                float trailingSpacing = isLastGid ? advance : 0f;
                float glyphRightAdvance = rightEdge - xCursor + trailingSpacing;
                float advacementToEnd = isVertical ? -(info.Advancement + trailingSpacing) : glyphRightAdvance;
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
