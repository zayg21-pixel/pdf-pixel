using HarfBuzzSharp;
using PdfReader.Fonts;
using PdfReader.Models;
using System;
using System.Collections.Generic;

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
        public static ShapedGlyph[] ShapeText(ref PdfText text, Font harfBuzzFont, string unicode, PdfFontBase font, PdfGraphicsState state)
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
                return ShapeUnicodeText(text, harfBuzzFont, unicode, font, state);
            }
        }

        private static ShapedGlyph[] ShapeUnicodeText(PdfText text, Font harfBuzzFont, string unicode, PdfFontBase font, PdfGraphicsState state)
        {
            float xOffset = 0;
            float yOffset = 0;

            HarfBuzzSharp.Buffer buffer = new HarfBuzzSharp.Buffer();
            buffer.ContentType = ContentType.Unicode;
            HashSet<uint> spaceClusters = new HashSet<uint>();
            uint cluster = 0;

            for (int i = 0; i < unicode.Length; i++)
            {
                if (i < unicode.Length - 1 && char.IsSurrogatePair(unicode[i], unicode[i + 1]))
                {
                    var codepoint = (uint)char.ConvertToUtf32(unicode[i], unicode[i + 1]);
                    buffer.Add(codepoint, cluster);
                    i++;
                }
                else
                {
                    buffer.Add(unicode[i], cluster);

                    if (unicode[i] == ' ')
                    {
                        spaceClusters.Add(cluster);
                    }
                }

                cluster++;
            }

            buffer.Direction = Direction.LeftToRight;
            harfBuzzFont.Shape(buffer);

            var result = new ShapedGlyph[buffer.Length];

            for (int i = 0; i < buffer.Length; i++)
            {
                var info = buffer.GlyphInfos[i];
                var position = buffer.GlyphPositions[i];

                var xNormalized = position.XAdvance / 64f;
                var yNormalized = position.YAdvance / 64f;

                result[i] = new ShapedGlyph(info.Codepoint, xOffset, yOffset, xNormalized, yNormalized);

                xOffset += xNormalized + state.CharacterSpacing;
                yOffset += yNormalized;

                if (state.WordSpacing != 0)
                {
                    if (spaceClusters.Contains(info.Cluster))
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
                harfBuzzFont.GetGlyphAdvanceForDirection(codepoint, Direction.LeftToRight, out int xAdvance, out int yAdvance);
                var xNormalized = xAdvance / 64f;
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