using Microsoft.Extensions.Logging;
using PdfReader.Fonts;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using PdfReader.Text;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Standard text drawer without HarfBuzz dependency.
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

            if (state.SoftMask != null)
            {
                SKRect measuredBounds;
                using (var softMaskScope = new SoftMaskDrawingScope(canvas, state, page))
                {
                    softMaskScope.BeginDrawContent();
                    measuredBounds = DrawTextInternal(canvas, ref pdfText, page, state, font, false);
                    softMaskScope.EndDrawContent();
                }
                return measuredBounds.Width;
            }

            var bounds = DrawTextInternal(canvas, ref pdfText, page, state, font, false);
            return bounds.Width;
        }

        /// <summary>
        /// Internal draw implementation. Assumes validated arguments (no defensive checks).
        /// </summary>
        private SKRect DrawTextInternal(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font, bool dryRun)
        {
            var typeface = _fontCache.GetTypeface(font);
            var unicodeText = _pdfTextDecoder.DecodeTextStringWithFont(pdfText.RawBytes, font);

            using var skPaint = PdfPaintFactory.CreateTextPaint(state, page);
            using var skFont = PdfPaintFactory.CreateTextFont(state, typeface, page);

            bool canShape = ShouldShapeText(font);

            SKSize size;
            if (canShape)
            {
                var shaped = BuildDirectMappedGlyphs(ref pdfText, skFont, state, font, typeface);
                size = DrawShapedText(canvas, skPaint, skFont, shaped, state, dryRun);
            }
            else
            {
                size = DrawUnicodeText(canvas, skPaint, skFont, unicodeText, state, dryRun);
            }

            var bounds = CalculateSoftMaskBounds(size);
            return bounds;
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
        private bool ShouldShapeText(PdfFontBase font)
        {
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
                case PdfCIDFont cidFont:
                {
                    if (cidFont.HasCIDToGIDMapping)
                    {
                        return true;
                    }
                    if (cidFont.Type == PdfFontType.CIDFontType2)
                    {
                        return true;
                    }
                    return false;
                }
                case PdfSimpleFont simpleFont:
                {
                    // Simple single-byte fonts have a direct, reliable mapping (Differences + encoding).
                    // Allow shaping so we can unify drawing path (still uses direct gid extraction).
                    return true;
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

        /// <summary>
        /// Build shaped glyphs using direct mapping.
        /// Returns empty if any GID invalid (0 or out-of-range) or all advances zero.
        /// </summary>
        private ShapedGlyph[] BuildDirectMappedGlyphs(ref PdfText pdfText, SKFont skFont, PdfGraphicsState state, PdfFontBase font, SKTypeface typeface)
        {
            var codes = _pdfTextDecoder.ExtractCharacterCodes(pdfText.RawBytes, font);
            var gids = pdfText.GetGids(codes, font);

            int glyphCountInTypeface = typeface.GlyphCount;
            int inputCount = gids.Length;
            var glyphArrayForWidth = new ushort[inputCount];
            for (int glyphIndex = 0; glyphIndex < inputCount; glyphIndex++)
            {
                uint gid = gids[glyphIndex];
                if (gid == 0 || gid >= glyphCountInTypeface)
                {
                    return Array.Empty<ShapedGlyph>();
                }
                glyphArrayForWidth[glyphIndex] = (ushort)gid;
            }

            float[] glyphWidths = skFont.GetGlyphWidths(glyphArrayForWidth);
            var shaped = new ShapedGlyph[inputCount];
            float cursorX = 0f;

            for (int index = 0; index < inputCount; index++)
            {
                uint gid = gids[index];
                float width = glyphWidths != null && index < glyphWidths.Length ? glyphWidths[index] : 0f;
                string unicodeForCode = _pdfTextDecoder.DecodeCharacterCode(codes[index], font);
                bool isSpace = unicodeForCode == " ";
                float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);
                float advance = width + spacing;
                shaped[index] = new ShapedGlyph(gid, cursorX, 0f, advance, 0f);
                cursorX += advance;
            }

            return shaped;
        }

        private SKSize DrawUnicodeText(SKCanvas canvas, SKPaint paint, SKFont font, string text, PdfGraphicsState state, bool dryRun)
        {
            if (string.IsNullOrEmpty(text))
            {
                return SKSize.Empty;
            }

            float advanceWidth = 0f;
            for (int index = 0; index < text.Length; index++)
            {
                string glyphString = text[index].ToString();
                if (!dryRun && state.TextRenderingMode != PdfTextRenderingMode.Invisible)
                {
                    canvas.DrawText(glyphString, advanceWidth, 0f, font, paint);
                }
                float measured = font.MeasureText(glyphString, paint);
                advanceWidth += measured + state.CharacterSpacing;
                if (glyphString == " ")
                {
                    advanceWidth += state.WordSpacing;
                }
            }

            var metrics = font.Metrics;
            float height = metrics.Descent - metrics.Ascent;
            paint.Dispose();
            return new SKSize(advanceWidth, height);
        }

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
                    if (shapedGlyph.GlyphId != 0)
                    {
                        advanceWidth += shapedGlyph.AdvanceX;
                    }
                }
            }

            var metrics = font.Metrics;
            float height = metrics.Descent - metrics.Ascent;
            paint.Dispose();
            return new SKSize(advanceWidth, height);
        }

        private SKRect CalculateSoftMaskBounds(SKSize size)
        {
            float top = -size.Height * 0.8f;
            float bottom = size.Height * 0.2f;
            // TODO: remove, calculate correct bounds
            return new SKRect(0f, top, size.Width, bottom);
        }
    }
}