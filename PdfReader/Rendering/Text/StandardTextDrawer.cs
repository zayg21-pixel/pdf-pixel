using PdfReader.Fonts;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using PdfReader.Rendering.HarfBuzz;
using PdfReader.Text;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Text
{
    public class StandardTextDrawer : ITextDrawer
    {
        private readonly IFontCache _fontCache;

        internal StandardTextDrawer(IFontCache fontCache)
        {
            _fontCache = fontCache ?? throw new ArgumentNullException(nameof(fontCache));
        }

        public float DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            // If nothing to draw, return immediately
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
                // No soft mask: draw directly and return width
                var actualBounds = DrawText(canvas, ref pdfText, page, state, font, dryRun: false);
                return actualBounds.Width;
            }
        }

        // Inner method: draws text or measures it, returns local bounds. Does NOT apply soft mask.
        // Returns SKRect.Empty only when there is truly no text to consider (empty text or unsupported font).
        private SKRect DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font, bool dryRun)
        {
            if (font?.Type == PdfFontType.Type3 || pdfText.IsEmpty)
            {
                return SKRect.Empty;
            }

            var typeface = _fontCache.GetTypeface(font);
            var harfBuzzFont = _fontCache.GetHarfBuzzFont(font);
            var unicodeText = pdfText.GetUnicodeText(font);
            bool isCffNameKeyed = font.FontDescriptor?.IsCffFont == true;


            using var skPpaint = PdfPaintFactory.CreateTextPaint(state, page);
            using var skFont = PdfPaintFactory.CreateTextFont(state, typeface, page);

            SKSize size;

            if (isCffNameKeyed)
            {
                var shaped = BuildCffShapedGlyphs(pdfText, skFont, state, font);
                size = DrawShapedText(canvas, skPpaint, skFont, shaped, state, dryRun);
            } else if (harfBuzzFont != null)
            {
                var shaped = HarfBuzzFontRenderer.ShapeText(ref pdfText, harfBuzzFont, font, state);
                size = DrawShapedText(canvas, skPpaint, skFont, shaped, state, dryRun);
            }
            else
            {
                size = DrawUnicodeText(canvas, skPpaint, skFont, unicodeText, state, dryRun);
            }

            // Compute a reasonable local bounds from measured size
            var bounds = CalculateSoftMaskBounds(size);
            return bounds;
        }

        private ShapedGlyph[] BuildCffShapedGlyphs(PdfText pdfText, SKFont font, PdfGraphicsState state, PdfFontBase baseFont)
        {
            var codes = pdfText.GetCharacterCodes(baseFont);
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
                string unicodeTextForCid = PdfTextDecoder.DecodeCharacterCode(codes[i], baseFont);
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

        // Compute a reasonable soft-mask bounds rectangle from measured size
        private SKRect CalculateSoftMaskBounds(SKSize size)
        {
            float top = -size.Height * 0.8f;
            float bottom = size.Height * 0.2f;
            return new SKRect(0, top, size.Width, bottom);
        }
    }
}