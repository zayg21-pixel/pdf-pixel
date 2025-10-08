using HarfBuzzSharp;
using PdfReader.Fonts;
using PdfReader.Models;
using System;

namespace PdfReader.Rendering.HarfBuzz
{
    /// <summary>
    /// Advanced font rendering system using HarfBuzz for comprehensive embedded font support
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public class HarfBuzzFontRenderer
    {
        /// <summary>
        /// Shape text using HarfBuzz for complex scripts and advanced typography
        /// Handles CID fonts with raw codepoints vs Unicode fonts
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public static ShapedGlyph[] ShapeText(ref PdfText text, Font harfBuzzFont, PdfFontBase font, PdfGraphicsState state)
        {
            if (text.IsEmpty)
                return Array.Empty<ShapedGlyph>();

            // Set font scale based on desired size
            var scale = (int)(state.FontSize * 64f); // Use 64 units per point for better precision
            harfBuzzFont.SetScale(scale, scale);

            // Determine if this is a CID font based on font hierarchy
            bool isCIDFont = font is PdfCIDFont || font is PdfCompositeFont || font.FontDescriptor?.IsCffFont == true;

            if (isCIDFont)
            {
                return ShapeGlyphs(text, harfBuzzFont, font, state);
            }
            else
            {
                return ShapeUnicodeText(text, harfBuzzFont, font, state);
            }
        }

        private static ShapedGlyph[] ShapeUnicodeText(PdfText text, Font harfBuzzFont, PdfFontBase font, PdfGraphicsState state)
        {
            float xOffset = 0;
            float yOffset = 0;

            // TODO: we can't really rely on this to calculate space advances, need to split char by char instead.
            string unicode = text.GetUnicodeText(font);
            HarfBuzzSharp.Buffer buffer = new HarfBuzzSharp.Buffer();
            buffer.AddUtf8(unicode);

            buffer.Direction = Direction.LeftToRight;
            harfBuzzFont.Shape(buffer);

            var result = new ShapedGlyph[buffer.Length];

            for (int i = 0; i < buffer.Length; i++)
            {
                var info = buffer.GlyphInfos[i];
                var position = buffer.GlyphPositions[i];

                // TODO: pass correct direction based on text direction
                var xNormalized = position.XAdvance / 64f; // HarfBuzz uses 26.6 fixed point
                var yNormalized = position.YAdvance / 64f;

                result[i] = new ShapedGlyph(info.Codepoint, xOffset, yOffset, xNormalized, yNormalized);

                xOffset += xNormalized + state.CharacterSpacing;
                yOffset += yNormalized;

                if (state.WordSpacing != 0 && info.Cluster < unicode.Length)
                {
                    if (unicode[(int)info.Cluster] == ' ')
                    {
                        xOffset += state.WordSpacing;
                    }
                }
            }

            return result;
        }

        private static ShapedGlyph[] ShapeGlyphs(PdfText text, Font harfBuzzFont, PdfFontBase font, PdfGraphicsState state)
        {
            var cids = text.GetCharacterCodes(font);
            var glyphs = text.GetGids(cids, font);

            float xOffset = 0;
            float yOffset = 0;
            var result = new ShapedGlyph[glyphs.Length];

            for (int i = 0; i < glyphs.Length; i++)
            {
                uint codepoint = glyphs[i];
                // TODO: pass correct direction based on text direction
                harfBuzzFont.GetGlyphAdvanceForDirection(codepoint, Direction.LeftToRight, out int xAdvance, out int yAdvance);
                var xNormalized = xAdvance / 64f; // HarfBuzz uses 26.6 fixed point
                var yNormalized = yAdvance / 64f;

                result[i] = new ShapedGlyph(codepoint, xOffset, yOffset, xNormalized, yNormalized);

                xOffset += xNormalized + state.CharacterSpacing;
                yOffset += yNormalized;

                if (state.WordSpacing != 0)
                {
                    string unicodeChars = text.GetUnicodeText(cids[i], font);

                    if (unicodeChars.IndexOf(' ') >= 0)
                    {
                        xOffset += state.WordSpacing;
                    }
                }
            }

            return result;
        }
    }
}