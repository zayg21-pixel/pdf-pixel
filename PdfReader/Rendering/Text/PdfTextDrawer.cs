using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Types;
using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Manages text drawing with proper drawer selection and positioning
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public class PdfTextDrawer : IPdfTextDrawer
    {
        private readonly IFontCache _fontCache;
        private readonly ILogger<PdfTextDrawer> _logger;

        internal PdfTextDrawer(IFontCache fontCache, ILoggerFactory loggerFactory)
        {
            _fontCache = fontCache ?? throw new ArgumentNullException(nameof(fontCache));
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            _logger = loggerFactory.CreateLogger<PdfTextDrawer>();
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

            var typeface = _fontCache.GetTypeface(font);
            using var skPaint = PdfPaintFactory.CreateTextPaint(state, page);
            using var skFont = PdfPaintFactory.CreateTextFont(state, typeface);

            var shaped = ShapeText(ref pdfText, state, font);

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);

            softMaskScope.BeginDrawContent();
            var size = DrawShapedText(canvas, skPaint, skFont, shaped, state, dryRun: false);
            softMaskScope.EndDrawContent();

            return size.Width;
        }

        /// <summary>
        /// Draw text with positioning adjustments (TJ operator) and return total advancement
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public float DrawTextWithPositioning(SKCanvas canvas, IPdfValue arrayOperand, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            if (arrayOperand.Type != PdfValueType.Array)
            {
                return 0f;
            }

            var array = arrayOperand.AsArray();
            if (array == null)
            {
                return 0f;
            }

            var shapedGlyphs = new List<ShapedGlyph>();
            float totalAdvancement = 0f;
            bool hasGlyphs = false;

            // Create typeface and skFont once
            var typeface = _fontCache.GetTypeface(font);
            using var skFont = PdfPaintFactory.CreateTextFont(state, typeface);

            for (int i = 0; i < array.Count; i++)
            {
                var item = array.GetValue(i);
                if (item == null)
                {
                    continue;
                }

                if (item.Type == PdfValueType.String || item.Type == PdfValueType.HexString)
                {
                    var pdfText = PdfText.FromOperand(item);
                    if (!pdfText.IsEmpty)
                    {
                        // Shape and add glyphs
                        var glyphs = ShapeText(ref pdfText, state, font);
                        shapedGlyphs.AddRange(glyphs);
                        hasGlyphs = true;
                    }
                }
                else
                {
                    // Positioning adjustment
                    var adjustment = item.AsFloat();
                    var adjustmentInUserSpace = -adjustment * state.FontSize / 1000f;
                    if (shapedGlyphs.Count > 0)
                    {
                        // Add advance to last glyph
                        var last = shapedGlyphs[shapedGlyphs.Count - 1];
                        shapedGlyphs[shapedGlyphs.Count - 1] = new ShapedGlyph(last.GlyphId, last.Width, last.AdvanceAfter + adjustmentInUserSpace);
                    }
                    else
                    {
                        // No glyphs yet, translate canvas
                        canvas.Translate(adjustmentInUserSpace, 0);
                        totalAdvancement += adjustmentInUserSpace;
                    }
                }
            }

            if (hasGlyphs && shapedGlyphs.Count > 0)
            {
                using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);

                softMaskScope.BeginDrawContent();

                // Draw all glyphs at once
                using var skPaint = PdfPaintFactory.CreateTextPaint(state, page);
                var size = DrawShapedText(canvas, skPaint, skFont, shapedGlyphs.ToArray(), state, false);
                totalAdvancement += size.Width;

                softMaskScope.EndDrawContent();
            }

            return totalAdvancement;
        }

        /// <summary>
        /// Shapes text by extracting character codes and character info for each code.
        /// Handles both direct mapping and shaping cases using unified logic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ShapedGlyph[] ShapeText(ref PdfText pdfText, PdfGraphicsState state, PdfFontBase font)
        {
            var codes = font.ExtractCharacterCodes(pdfText.RawBytes);
            var shapedGlyphs = new List<ShapedGlyph>(codes.Length);

            for (int codeIndex = 0; codeIndex < codes.Length; codeIndex++)
            {
                PdfCharacterInfo info = font.ExtractCharacterInfo(codes[codeIndex]);
                string unicode = info.Unicode;
                bool isSpace = unicode == " ";
                float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);

                shapedGlyphs.Add(new ShapedGlyph(info.Gid, info.Width * state.FontSize, spacing));
            }

            return shapedGlyphs.ToArray();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private SKSize DrawShapedText(SKCanvas canvas, SKPaint paint, SKFont font, ShapedGlyph[] shapingResult, PdfGraphicsState state, bool dryRun)
        {
            if (shapingResult.Length == 0)
            {
                return SKSize.Empty;
            }

            float advanceWidth = 0f;
            float x = 0f;
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
                    positionSpan[index] = new SKPoint(x, 0f);
                    advanceWidth += shapedGlyph.TotalWidth;
                    x += shapedGlyph.TotalWidth;
                }
                using var blob = builder.Build();
                canvas.DrawText(blob, 0f, 0f, paint);
            }
            else
            {
                for (int index = 0; index < shapingResult.Length; index++)
                {
                    ref var shapedGlyph = ref shapingResult[index];
                    advanceWidth += shapedGlyph.TotalWidth;
                }
            }

            var metrics = font.Metrics;
            float height = metrics.Descent - metrics.Ascent;

            return new SKSize(advanceWidth, height);
        }
    }
}