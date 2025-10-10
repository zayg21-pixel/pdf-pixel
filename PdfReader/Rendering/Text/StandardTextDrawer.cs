using Microsoft.Extensions.Logging;
using PdfReader.Fonts;
using PdfReader.Fonts.Management;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using PdfReader.Rendering.HarfBuzz;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Linq;

namespace PdfReader.Rendering.Text
{
    public class StandardTextDrawer : ITextDrawer
    {
        private readonly IFontCache _fontCache;
        private readonly ILogger<StandardTextDrawer> _logger;
        private readonly PdfTextDecoder _pdfTextDecoder;
        private readonly HarfBuzzFontRenderer _harfBuzzRenderer;

        internal StandardTextDrawer(IFontCache fontCache, ILoggerFactory loggerFactory)
        {
            _fontCache = fontCache ?? throw new ArgumentNullException(nameof(fontCache));
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _pdfTextDecoder = new PdfTextDecoder(loggerFactory);
            _harfBuzzRenderer = new HarfBuzzFontRenderer(_pdfTextDecoder);
            _logger = loggerFactory.CreateLogger<StandardTextDrawer>();
        }

        public float DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            if (font?.Type == PdfFontType.Type3 || pdfText.IsEmpty)
            {
                return 0f;
            }

            if (state.SoftMask != null)
            {
                var measuredBounds = DrawText(canvas, ref pdfText, page, state, font, dryRun: true);
                if (measuredBounds.IsEmpty)
                {
                    return 0f;
                }

                using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page, measuredBounds);
                softMaskScope.BeginDrawContent();
                DrawText(canvas, ref pdfText, page, state, font, dryRun: false);
                softMaskScope.EndDrawContent();
                return measuredBounds.Width;
            }
            else
            {
                var actualBounds = DrawText(canvas, ref pdfText, page, state, font, dryRun: false);
                return actualBounds.Width;
            }
        }

        private SKRect DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font, bool dryRun)
        {
            if (font?.Type == PdfFontType.Type3 || pdfText.IsEmpty)
            {
                return SKRect.Empty;
            }

            var typeface = _fontCache.GetTypeface(font);
            var harfBuzzFont = _fontCache.GetHarfBuzzFont(font);
            var unicodeText = _pdfTextDecoder.DecodeTextStringWithFont(pdfText.RawBytes, font);
            bool isCffNameKeyed = font.FontDescriptor?.IsCffFont == true;

            using var skPaint = PdfPaintFactory.CreateTextPaint(state, page);
            using var skFont = PdfPaintFactory.CreateTextFont(state, typeface, page);

            SKSize size;
            if (isCffNameKeyed)
            {
                var shaped = BuildCffShapedGlyphs(ref pdfText, skFont, state, font);
                size = DrawShapedText(canvas, skPaint, skFont, shaped, state, dryRun);
            }
            else if (harfBuzzFont != null)
            {
                var shaped = _harfBuzzRenderer.ShapeText(ref pdfText, harfBuzzFont, unicodeText, font, state);
                if (shaped.All(x => x.GlyphId == 0))
                {
                    size = DrawUnicodeText(canvas, skPaint, skFont, unicodeText, state, dryRun);
                }
                else
                {
                    size = DrawShapedText(canvas, skPaint, skFont, shaped, state, dryRun);
                }
            }
            else
            {
                size = DrawUnicodeText(canvas, skPaint, skFont, unicodeText, state, dryRun);
            }

            var bounds = CalculateSoftMaskBounds(size);
            return bounds;
        }

        private ShapedGlyph[] BuildCffShapedGlyphs(ref PdfText pdfText, SKFont font, PdfGraphicsState state, PdfFontBase baseFont)
        {
            var codes = _pdfTextDecoder.ExtractCharacterCodes(pdfText.RawBytes, baseFont);
            var gids = pdfText.GetGids(codes, baseFont);

            int glyphCount = gids.Length;
            var glyphArray = new ushort[glyphCount];
            for (int i = 0; i < glyphCount; i++)
            {
                glyphArray[i] = (ushort)gids[i];
            }

            float[] glyphWidths = font.GetGlyphWidths(glyphArray);
            var shaped = new ShapedGlyph[glyphCount];
            int shapeIndex = 0;
            float cursorX = 0f;
            for (int i = 0; i < glyphCount; i++)
            {
                uint gid = gids[i];
                if (gid == 0)
                {
                    continue;
                }
                float width = (gid != 0 && glyphWidths != null && i < glyphWidths.Length) ? glyphWidths[i] : 0f;
                string unicodeTextForCid = _pdfTextDecoder.DecodeCharacterCode(codes[i], baseFont);
                float extra = state.CharacterSpacing + (unicodeTextForCid == " " ? state.WordSpacing : 0f);
                shaped[shapeIndex] = new ShapedGlyph(gid, cursorX, 0, width + extra, 0);
                cursorX += width + extra;
                shapeIndex++;
            }
            if (shapeIndex != shaped.Length)
            {
                Array.Resize(ref shaped, shapeIndex);
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
            for (int i = 0; i < text.Length; i++)
            {
                string glyphString = text[i].ToString();
                if (!dryRun && state.TextRenderingMode != PdfTextRenderingMode.Invisible)
                {
                    canvas.DrawText(glyphString, advanceWidth, 0, font, paint);
                }
                advanceWidth += font.MeasureText(glyphString, paint) + state.CharacterSpacing;
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
                for (int i = 0; i < shapingResult.Length; i++)
                {
                    ref var shapedGlyph = ref shapingResult[i];
                    glyphSpan[i] = (ushort)shapedGlyph.GlyphId;
                    positionSpan[i] = new SKPoint(shapedGlyph.X, shapedGlyph.Y);
                    advanceWidth += shapedGlyph.AdvanceX;
                }
                using var blob = builder.Build();
                canvas.DrawText(blob, 0, 0, paint);
            }
            else
            {
                for (int i = 0; i < shapingResult.Length; i++)
                {
                    ref var shapedGlyph = ref shapingResult[i];
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
            return new SKRect(0, top, size.Width, bottom);
        }
    }
}