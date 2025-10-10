using HarfBuzzSharp;
using PdfReader.Fonts;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Text;
using System;
using System.Collections.Generic;

namespace PdfReader.Rendering.HarfBuzz
{
    /// <summary>
    /// HarfBuzz based text shaping for complex scripts and advanced OpenType features.
    /// Instance-based so it can reuse a shared PdfTextDecoder (for CID->Unicode fallbacks etc.).
    /// </summary>
    public sealed class HarfBuzzFontRenderer
    {
        private readonly PdfTextDecoder _decoder;

        public HarfBuzzFontRenderer(PdfTextDecoder decoder)
        {
            _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        }

        /// <summary>
        /// Shape text using HarfBuzz for complex scripts and advanced typography.
        /// Handles CID fonts with raw codes vs Unicode fonts.
        /// </summary>
        public ShapedGlyph[] ShapeText(ref PdfText text, Font harfBuzzFont, string unicode, PdfFontBase font, PdfGraphicsState state)
        {
            if (text.IsEmpty)
            {
                return Array.Empty<ShapedGlyph>();
            }

            int scale = (int)(state.FontSize * 64f);
            harfBuzzFont.SetScale(scale, scale);

            bool isCidFont = font is PdfCIDFont || font is PdfCompositeFont || font.FontDescriptor?.IsCffFont == true;
            if (isCidFont)
            {
                return ShapeGlyphs(ref text, harfBuzzFont, font, state);
            }
            else
            {
                return ShapeUnicodeText(unicode, harfBuzzFont, state);
            }
        }

        private ShapedGlyph[] ShapeUnicodeText(string unicode, Font harfBuzzFont, PdfGraphicsState state)
        {
            float xOffset = 0f;
            float yOffset = 0f;

            var buffer = new HarfBuzzSharp.Buffer
            {
                ContentType = ContentType.Unicode,
                Direction = Direction.LeftToRight
            };

            var spaceClusters = new HashSet<uint>();
            uint cluster = 0;

            for (int i = 0; i < unicode.Length; i++)
            {
                if (i < unicode.Length - 1 && char.IsSurrogatePair(unicode[i], unicode[i + 1]))
                {
                    uint codepoint = (uint)char.ConvertToUtf32(unicode[i], unicode[i + 1]);
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

            harfBuzzFont.Shape(buffer);
            var result = new ShapedGlyph[buffer.Length];

            for (int i = 0; i < buffer.Length; i++)
            {
                var info = buffer.GlyphInfos[i];
                var position = buffer.GlyphPositions[i];
                float xAdvance = position.XAdvance / 64f;
                float yAdvance = position.YAdvance / 64f;

                result[i] = new ShapedGlyph(info.Codepoint, xOffset, yOffset, xAdvance, yAdvance);

                xOffset += xAdvance + state.CharacterSpacing;
                yOffset += yAdvance;

                if (state.WordSpacing != 0 && spaceClusters.Contains(info.Cluster))
                {
                    xOffset += state.WordSpacing;
                }
            }

            return result;
        }

        private ShapedGlyph[] ShapeGlyphs(ref PdfText text, Font harfBuzzFont, PdfFontBase font, PdfGraphicsState state)
        {
            var codes = _decoder.ExtractCharacterCodes(text.RawBytes, font);
            var gids = text.GetGids(codes, font);

            float xOffset = 0f;
            float yOffset = 0f;
            var result = new ShapedGlyph[gids.Length];

            for (int i = 0; i < gids.Length; i++)
            {
                uint gid = gids[i];
                harfBuzzFont.GetGlyphAdvanceForDirection(gid, Direction.LeftToRight, out int xAdv, out int yAdv);
                float xAdvance = xAdv / 64f;
                float yAdvance = yAdv / 64f;

                result[i] = new ShapedGlyph(gid, xOffset, yOffset, xAdvance, yAdvance);

                xOffset += xAdvance + state.CharacterSpacing;
                yOffset += yAdvance;

                if (state.WordSpacing != 0)
                {
                    string unicodeChars = _decoder.DecodeCharacterCode(codes[i], font);
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