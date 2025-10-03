using HarfBuzzSharp;
using PdfReader.Fonts;
using PdfReader.Models;
using System;
using System.Runtime.InteropServices;

namespace PdfReader.Rendering.HarfBuzz
{
    /// <summary>
    /// Advanced font rendering system using HarfBuzz for comprehensive embedded font support
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public static class HarfBuzzFontRenderer
    {
        public static Font CreateHarfBuzzFont(PdfFontBase pdfFont)
        {
            if (pdfFont == null || !pdfFont.IsEmbedded)
                return null;

            try
            {
                // Get or create face based on font type
                var face = CreateHarfBuzzFace(pdfFont);
                if (face == null)
                    return null;

                // Create font from face
                var font = new Font(face);

                // Set font size (will be scaled later based on actual text size)
                font.SetScale(1000, 1000); // Use 1000 units per em as base

                return font;
            }
            catch
            {
                return null;
            }
        }

        private static Face CreateHarfBuzzFace(PdfFontBase pdfFont)
        {
            if (pdfFont == null || !pdfFont.IsEmbedded)
                return null;

            // Get font data based on font type and descriptor
            ReadOnlyMemory<byte> fontData = default;
            switch (pdfFont)
            {
                case PdfSimpleFont simpleFont:
                    fontData = simpleFont.FontDescriptor?.GetFontStream() ?? default;
                    break;
                case PdfCIDFont cidFont:
                    fontData = cidFont.FontDescriptor?.GetFontStream() ?? default;
                    break;
                case PdfCompositeFont compositeFont:
                    // For composite fonts, get data from primary descendant
                    var primaryDescendant = compositeFont.PrimaryDescendant;
                    if (primaryDescendant?.IsEmbedded == true)
                    {
                        fontData = primaryDescendant.FontDescriptor?.GetFontStream() ?? default;
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Composite font {compositeFont.BaseFont} has no embedded descendant font");
                        return null;
                    }
                    break;
                case PdfType3Font:
                    // Type3 fonts don't have embedded font files - they use procedural glyphs
                    Console.WriteLine($"Warning: Type3 font {pdfFont.BaseFont} cannot be used with HarfBuzz");
                    return null;
                default:
                    Console.WriteLine($"Warning: Unknown font type {pdfFont.GetType().Name} for HarfBuzz font creation");
                    return null;
            }

            if (fontData.IsEmpty)
            {
                Console.WriteLine($"Warning: Font {pdfFont.BaseFont} has no embedded font data");
                return null;
            }

            // GetFontStream already returns decoded and wrapped data when necessary
            var fontDataArray = fontData.ToArray();

            // Pin the data in memory for HarfBuzz
            var handle = GCHandle.Alloc(fontDataArray, GCHandleType.Pinned);
            var ptr = handle.AddrOfPinnedObject();

            // Create blob with pinned data
            var blob = new Blob(ptr, fontDataArray.Length, MemoryMode.ReadOnly, () => handle.Free());

            // Create face from blob
            var face = new Face(blob, 0); // 0 = first face in font file
            face.MakeImmutable();

            if (face.GlyphCount == 0)
            {
                face.Dispose();
                Console.WriteLine($"Warning: Font {pdfFont.BaseFont} has invalid font data (no glyphs)");
                return null;
            }

            return face;
        }

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