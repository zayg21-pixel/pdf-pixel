using Microsoft.Extensions.Logging;
using PdfReader.Fonts;
using PdfReader.Fonts.Management;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Text
{
    /// <summary>.
    /// Performs direct CID/composite glyph mapping when reliable; otherwise falls back to Unicode drawing.
    /// </summary>
    public class StandardTextDrawer : ITextDrawer
    {
        private readonly IFontCache _fontCache;
        private readonly ILogger<StandardTextDrawer> _logger;
        private readonly PdfTextDecoder _pdfTextDecoder;

        internal StandardTextDrawer(IFontCache fontCache, ILoggerFactory loggerFactory)
        {
            _fontCache = fontCache ?? throw new ArgumentNullException(nameof(fontCache));
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _pdfTextDecoder = new PdfTextDecoder(loggerFactory);
            _logger = loggerFactory.CreateLogger<StandardTextDrawer>();
        }

        /// <summary>
        /// Draw PDF text and return the advancement width in user space units.
        /// Validates arguments (public API guard clauses).
        /// </summary>
        public float DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            if (font == null)
            {
                return 0f;
            }
            if (font.Type == PdfFontType.Type3 || pdfText.IsEmpty)
            {
                return 0f;
            }

            SKSize size;

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);
            softMaskScope.BeginDrawContent();
            size = DrawTextInternal(canvas, ref pdfText, page, state, font, false);
            softMaskScope.EndDrawContent();

            return size.Width;
        }

        /// <summary>
        /// Internal draw implementation. Assumes validated arguments (no defensive checks).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SKSize DrawTextInternal(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font, bool dryRun)
        {
            var typeface = _fontCache.GetTypeface(font);
            using var skPaint = PdfPaintFactory.CreateTextPaint(state, page);
            using var skFont = PdfPaintFactory.CreateTextFont(state, typeface, page);

            bool canShape = ShouldShapeText(font);

            SKSize size;
            if (canShape)
            {
                var shaped = BuildDirectMappedGlyphs(ref pdfText, skFont, state, font);
                size = DrawShapedText(canvas, skPaint, skFont, shaped, state, dryRun);
            }
            else
            {
                var shaped = ShapeUnicodeText(ref pdfText, skFont, state, font);
                size = DrawShapedText(canvas, skPaint, skFont, shaped, state, dryRun);
            }

            return size;
        }

        /// <summary>
        /// Build shaped glyphs using direct mapping.
        /// Returns empty if any GID invalid (0 or out-of-range) or all advances zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ShapedGlyph[] BuildDirectMappedGlyphs(ref PdfText pdfText, SKFont skFont, PdfGraphicsState state, PdfFontBase font)
        {
            var codes = _pdfTextDecoder.ExtractCharacterCodes(pdfText.RawBytes, font);
            var gids = pdfText.GetGids(codes, font);

            // TODO: need to correctly build Width map (CIDWidths is never set), and only if widths is missing, fallback to font metrics.
            float[] glyphWidths = skFont.GetGlyphWidths(gids);

            var shaped = new ShapedGlyph[gids.Length];
            float cursorX = 0f;

            for (int index = 0; index < gids.Length; index++)
            {
                uint gid = gids[index];
                float width = glyphWidths[index];
                string unicodeForCode = _pdfTextDecoder.DecodeCharacterCode(codes[index], font);
                bool isSpace = unicodeForCode == " ";
                float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);
                float advance = width + spacing;
                shaped[index] = new ShapedGlyph(gid, cursorX, 0f, advance, 0f);
                cursorX += advance;
            }

            return shaped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ShapedGlyph[] ShapeUnicodeText(ref PdfText pdfText, SKFont skFont, PdfGraphicsState state, PdfFontBase font)
        {
            var codes = _pdfTextDecoder.ExtractCharacterCodes(pdfText.RawBytes, font);
            var shaped = new ShapedGlyph[codes.Length];
            int codeOffset = 0;
            float cursorX = 0f;

            for (int index = 0; index < codes.Length; index++)
            {
                string unicodeForCode = _pdfTextDecoder.DecodeCharacterCode(codes[index], font);
                var gids = skFont.GetGlyphs(unicodeForCode);
                var glyphWidths = skFont.GetGlyphWidths(gids);
                int offset = gids.Length - 1;

                if (offset > 0)
                {
                    Array.Resize(ref shaped, shaped.Length + offset);
                }

                for (int g = 0; g < gids.Length; g++)
                {
                    uint gid = gids[g];
                    float width = glyphWidths[g];
                    bool isSpace = unicodeForCode == " ";
                    float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);
                    float advance = width + spacing;
                    shaped[index + codeOffset] = new ShapedGlyph(gid, cursorX, 0f, advance, 0f);
                    cursorX += advance;
                }

                codeOffset += offset;
            }

            return shaped;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SKSize DrawShapedText(SKCanvas canvas, SKPaint paint, SKFont font, ShapedGlyph[] shapingResult, PdfGraphicsState state, bool dryRun)
        {
            if (shapingResult.Length == 0)
            {
                return SKSize.Empty;
            }

            float advanceWidth = 0f;
            if (!dryRun && state.TextRenderingMode != PdfTextRenderingMode.Invisible)
            {
                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(font, shapingResult.Length);
                var glyphSpan = run.Glyphs;
                var positionSpan = run.Positions;
                for (int index = 0; index < shapingResult.Length; index++)
                {
                    ref var shapedGlyph = ref shapingResult[index];
                    glyphSpan[index] = (ushort)shapedGlyph.GlyphId;
                    positionSpan[index] = new SKPoint(shapedGlyph.X, shapedGlyph.Y);
                    advanceWidth += shapedGlyph.AdvanceX;
                }
                using var blob = builder.Build();
                canvas.DrawText(blob, 0f, 0f, paint);
            }
            else
            {
                for (int index = 0; index < shapingResult.Length; index++)
                {
                    ref var shapedGlyph = ref shapingResult[index];
                    advanceWidth += shapedGlyph.AdvanceX;
                }
            }

            var metrics = font.Metrics;
            float height = metrics.Descent - metrics.Ascent;

            return new SKSize(advanceWidth, height);
        }

        /// <summary>
        /// Decide if we should attempt shaping (direct glyph mapping) for the font.
        /// Rules:
        /// - Composite (Type0): shape if descendant has CIDToGIDMap OR implicit Identity mapping for CIDFontType2.
        /// - CIDFont: shape if CIDToGIDMap present OR implicit identity (CIDFontType2).
        /// - Simple fonts (Type1/TrueType): shape (single-byte reliable mapping) unless font size/path indicates fallback need.
        /// - Type3: never shape (glyph defined by content stream; handled elsewhere).
        /// - Other / unknown: fallback to Unicode.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool ShouldShapeText(PdfFontBase font)
        {
            // TODO: should be a virtual property on PdfFontBase and overridden in subclasses
            switch (font)
            {
                case PdfCompositeFont compositeFont:
                    {
                        var descendant = compositeFont.PrimaryDescendant;
                        if (descendant == null)
                        {
                            return false;
                        }
                        bool identityEncoding = compositeFont.Encoding == PdfFontEncoding.IdentityH || compositeFont.Encoding == PdfFontEncoding.IdentityV;
                        if (descendant.HasCIDToGIDMapping)
                        {
                            return true;
                        }
                        if (descendant.Type == PdfFontType.CIDFontType2 && identityEncoding)
                        {
                            return true;
                        }
                        return false;
                    }
                case PdfSimpleFont simpleFont:
                    {
                        // Simple single-byte fonts have a direct, reliable mapping (Differences + encoding).
                        // however, currently we only have CFF mappings for simple fonts.
                        return simpleFont.FontDescriptor?.IsCffFont == true;
                    }
                case PdfType3Font _:
                    {
                        // Type3 glyphs are content streams; shaping not applicable here.
                        return false;
                    }
                default:
                    {
                        // Unknown / unsupported font category -> fallback to Unicode.
                        return false;
                    }
            }
        }
    }
}