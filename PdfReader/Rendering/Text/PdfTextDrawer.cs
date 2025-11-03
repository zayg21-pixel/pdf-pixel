using Microsoft.Extensions.Logging;
using PdfReader.Fonts;
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
            if (font.Type == PdfFontSubType.Type3 || pdfText.IsEmpty)
            {
                return 0f;
            }

            if (pdfText.IsEmpty)
            {
                return 0;
            }

            var typeface = _fontCache.GetTypeface(font);
            using var skPaint = PdfPaintFactory.CreateTextPaint(state, page);
            using var skFont = PdfPaintFactory.CreateTextFont(state, typeface);

            var shaped = ShapeText(ref pdfText, state, font);

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);

            softMaskScope.BeginDrawContent();
            var width = DrawShapedText(canvas, skPaint, skFont, shaped, state, dryRun: false);
            softMaskScope.EndDrawContent();

            return width * state.HorizontalScaling / 100f * state.FontSize; // TODO: it's unclear from specs, should we apply horizontal scaling here. Logically - yes as it's affects displacement.
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

                if (item.Type == PdfValueType.String)
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
                    var adjustmentInUserSpace = -adjustment / 1000f;

                    if (shapedGlyphs.Count > 0)
                    {
                        // Add advance to last glyph
                        var last = shapedGlyphs[shapedGlyphs.Count - 1];
                        shapedGlyphs[shapedGlyphs.Count - 1] = new ShapedGlyph(last.GlyphId, last.Width, last.AdvanceAfter + adjustmentInUserSpace);
                    }
                    else
                    {
                        shapedGlyphs.Insert(0, new ShapedGlyph(0, 0, adjustmentInUserSpace)); // TODO: fix, this will draw a glyph with ID 0, width is defined as 0, so it might not render, but...
                    }
                }
            }

            float totalAdvancement = 0f;

            if (hasGlyphs && shapedGlyphs.Count > 0)
            {
                using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);

                softMaskScope.BeginDrawContent();

                // Draw all glyphs at once
                using var skPaint = PdfPaintFactory.CreateTextPaint(state, page);
                totalAdvancement = DrawShapedText(canvas, skPaint, skFont, shapedGlyphs.ToArray(), state, false);

                softMaskScope.EndDrawContent();
            }

            return totalAdvancement * state.HorizontalScaling / 100f * state.FontSize;
        }

        /// <summary>
        /// Shapes text by extracting character codes and character info for each code.
        /// Handles both direct mapping and shaping cases using unified logic.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ShapedGlyph[] ShapeText(ref PdfText pdfText, PdfGraphicsState state, PdfFontBase font)
        {
            var codes = font.ExtractCharacterCodes(pdfText.RawBytes);
            ShapedGlyph[] shapedGlyphs = new ShapedGlyph[codes.Length];

            for (int codeIndex = 0; codeIndex < codes.Length; codeIndex++)
            {
                PdfCharacterInfo info = font.ExtractCharacterInfo(codes[codeIndex]);
                string unicode = info.Unicode;
                bool isSpace = unicode == " ";
                float spacing = state.CharacterSpacing + (isSpace ? state.WordSpacing : 0f);

                shapedGlyphs[codeIndex] = new ShapedGlyph(info.Gid, info.Width, spacing / state.FontSize);
            }

            return shapedGlyphs;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private float DrawShapedText(SKCanvas canvas, SKPaint paint, SKFont font, ShapedGlyph[] shapingResult, PdfGraphicsState state, bool dryRun)
        {
            if (shapingResult.Length == 0)
            {
                return 0;
            }

            var textMatrix = state.GetFullTextMatrix();

            float advanceWidth = 0f;

            // Draw visible text first (for combined modes) per PDF spec
            if (!dryRun && IsVisibleText(state.TextRenderingMode))
            {
                canvas.Save();

                // Apply text matrix transformation
                canvas.Concat(textMatrix);

                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(font, shapingResult.Length);
                var glyphSpan = run.Glyphs;
                var positionSpan = run.Positions;
                for (int index = 0; index < shapingResult.Length; index++)
                {
                    ref var shapedGlyph = ref shapingResult[index];
                    glyphSpan[index] = (ushort)shapedGlyph.GlyphId;
                    positionSpan[index] = new SKPoint(advanceWidth, 0f);
                    advanceWidth += shapedGlyph.TotalWidth;
                }
                using var blob = builder.Build();
                canvas.DrawText(blob, 0f, 0f, paint);

                canvas.Restore();
            }
            else
            {
                for (int index = 0; index < shapingResult.Length; index++)
                {
                    ref var shapedGlyph = ref shapingResult[index];
                    advanceWidth += shapedGlyph.TotalWidth;
                }
            }

            // Apply clipping if requested (modes with Clip). Pure clip mode skips drawing above.
            if (!dryRun && IsClippingText(state.TextRenderingMode))
            {
                var textPath = new SKPath();

                float x = 0f;
                for (int i = 0; i < shapingResult.Length; i++)
                {
                    var glyphId = shapingResult[i].GlyphId;
                    using var glyphPath = font.GetGlyphPath((ushort)glyphId);
                    if (glyphPath != null)
                    {
                        // Translate glyph outline by current advance
                        textPath.AddPath(glyphPath, SKMatrix.CreateTranslation(x, 0f));
                    }
                    x += shapingResult[i].TotalWidth;
                }

                textPath.Transform(textMatrix);

                if (state.TextClipPath == null)
                {
                    state.TextClipPath = new SKPath();
                }

                state.TextClipPath.AddPath(textPath);
            }

            return advanceWidth;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsVisibleText(PdfTextRenderingMode mode)
        {
            switch (mode)
            {
                case PdfTextRenderingMode.Fill:
                case PdfTextRenderingMode.Stroke:
                case PdfTextRenderingMode.FillAndStroke:
                case PdfTextRenderingMode.FillAndClip:
                case PdfTextRenderingMode.StrokeAndClip:
                case PdfTextRenderingMode.FillAndStrokeAndClip:
                    return true;
                case PdfTextRenderingMode.Invisible:
                case PdfTextRenderingMode.Clip:
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsClippingText(PdfTextRenderingMode mode)
        {
            switch (mode)
            {
                case PdfTextRenderingMode.Clip:
                case PdfTextRenderingMode.FillAndClip:
                case PdfTextRenderingMode.StrokeAndClip:
                case PdfTextRenderingMode.FillAndStrokeAndClip:
                    return true;
                case PdfTextRenderingMode.Fill:
                case PdfTextRenderingMode.Stroke:
                case PdfTextRenderingMode.FillAndStroke:
                case PdfTextRenderingMode.Invisible:
                default:
                    return false;
            }
        }
    }
}