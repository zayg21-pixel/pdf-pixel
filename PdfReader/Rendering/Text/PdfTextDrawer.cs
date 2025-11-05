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
            using var skFont = PdfPaintFactory.CreateTextFont(typeface);

            var shaped = ShapeText(ref pdfText, state, font);

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);

            softMaskScope.BeginDrawContent();
            var width = DrawShapedText(canvas, skFont, shaped, state);
            softMaskScope.EndDrawContent();

            return width;
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
            float initialAdvance = 0f;

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
                        shapedGlyphs.Insert(0, new ShapedGlyph(0, 0, initialAdvance));
                        initialAdvance = adjustmentInUserSpace;
                    }
                }
            }

            float width = 0f;

            if (shapedGlyphs.Count > 0)
            {
                using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page);

                softMaskScope.BeginDrawContent();

                // Create typeface and skFont once
                var typeface = _fontCache.GetTypeface(font);
                using var skFont = PdfPaintFactory.CreateTextFont(typeface);

                width = DrawShapedText(canvas, skFont, shapedGlyphs.ToArray(), state);

                softMaskScope.EndDrawContent();
            }

            return width;
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
        private float DrawShapedText(SKCanvas canvas, SKFont font, ShapedGlyph[] shapingResult, PdfGraphicsState state)
        {
            if (shapingResult.Length == 0)
            {
                return 0;
            }

            // this produces the combined text matrix with rise and font size applied, needed to draw outlines correctly
            var textMatrix = state.TextMatrix;

            if (state.Rise != 0)
            {
                textMatrix = SKMatrix.Concat(textMatrix, SKMatrix.CreateTranslation(0, state.Rise));
            }

            // Apply font size, horizontal scaling, and vertical flip
            var fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
            var fontScalingMatrix = SKMatrix.CreateScale(fullHorizontalScale, -state.FontSize);
            textMatrix = SKMatrix.Concat(textMatrix, fontScalingMatrix);


            float width = 0;

            if (ShouldFill(state.TextRenderingMode))
            {
                float currentAdvance = 0f;
                canvas.Save();

                // Apply text matrix transformation
                canvas.Concat(textMatrix);

                // Pre-count drawable glyphs (gid != 0) while computing positions using full advance including skipped glyphs.
                int drawableCount = 0;
                for (int i = 0; i < shapingResult.Length; i++)
                {
                    if (shapingResult[i].GlyphId != 0)
                    {
                        drawableCount++;
                    }
                }

                using var builder = new SKTextBlobBuilder();
                var run = builder.AllocatePositionedRun(font, drawableCount);
                var glyphSpan = run.Glyphs;
                var positionSpan = run.Positions;

                int drawIndex = 0;
                for (int index = 0; index < shapingResult.Length; index++)
                {
                    ref var shapedGlyph = ref shapingResult[index];
                    // Record position regardless to advance subsequent glyphs.
                    if (shapedGlyph.GlyphId != 0)
                    {
                        glyphSpan[drawIndex] = (ushort)shapedGlyph.GlyphId;
                        positionSpan[drawIndex] = new SKPoint(currentAdvance, 0f);
                        drawIndex++;
                    }

                    currentAdvance += shapedGlyph.TotalWidth;
                }

                using var blob = builder.Build();
                using var paint = PdfPaintFactory.CreateFillPaint(state);

                if (paint.Shader != null)
                {
                    // we need to adjust the shader matrix to account for the text matrix to avoid double-transforming
                    // currently shader only used for patterns, so this is sufficient, but may need more general handling later
                    paint.Shader = paint.Shader.WithLocalMatrix(textMatrix.Invert());
                }

                if (drawableCount > 0)
                {
                    canvas.DrawText(blob, 0f, 0f, paint);
                }

                canvas.Restore();

                width = Math.Max(width, currentAdvance);
            }

            if (ShouldStroke(state.TextRenderingMode))
            {
                // this is 4-5 times slower than drawing blob, but it's needed for correct stroking for PDF compliance
                // TODO: we can add special case here for simple fonts with linear transformations to use faster blob stroking
                var textPath = new SKPath();

                float x = 0f;

                for (int i = 0; i < shapingResult.Length; i++)
                {
                    var glyphId = shapingResult[i].GlyphId;
                    if (glyphId != 0)
                    {
                        using var glyphPath = font.GetGlyphPath((ushort)glyphId);
                        if (glyphPath != null)
                        {
                            // Translate glyph outline by current advance
                            textPath.AddPath(glyphPath, SKMatrix.CreateTranslation(x, 0f));
                        }
                    }

                    x += shapingResult[i].TotalWidth;
                }

                textPath.Transform(textMatrix);

                using var paint = PdfPaintFactory.CreateStrokePaint(state);
                if (!textPath.IsEmpty)
                {
                    canvas.DrawPath(textPath, paint);
                }

                width = Math.Max(width, x);
            }

            // Apply clipping if requested (modes with Clip). Pure clip mode skips drawing above.
            if (ShouldClip(state.TextRenderingMode))
            {
                var textPath = new SKPath();
                float x = 0f;

                for (int i = 0; i < shapingResult.Length; i++)
                {
                    var glyphId = shapingResult[i].GlyphId;
                    if (glyphId != 0)
                    {
                        using var glyphPath = font.GetGlyphPath((ushort)glyphId);
                        if (glyphPath != null)
                        {
                            // Translate glyph outline by current advance
                            textPath.AddPath(glyphPath, SKMatrix.CreateTranslation(x, 0f));
                        }
                    }

                    x += shapingResult[i].TotalWidth;
                }

                textPath.Transform(textMatrix);

                if (!textPath.IsEmpty)
                {
                    state.TextClipPath ??= new SKPath();
                    state.TextClipPath.AddPath(textPath);
                }

                width = Math.Max(width, x);
            }

            if (width == 0)
            {
                for (int index = 0; index < shapingResult.Length; index++)
                {
                    ref var shapedGlyph = ref shapingResult[index];
                    width += shapedGlyph.TotalWidth;
                }
            }

            return width * fullHorizontalScale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldFill(PdfTextRenderingMode mode)
        {
            switch (mode)
            {
                case PdfTextRenderingMode.Fill:
                case PdfTextRenderingMode.FillAndStroke:
                case PdfTextRenderingMode.FillAndClip:
                case PdfTextRenderingMode.FillAndStrokeAndClip:
                    return true;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldStroke(PdfTextRenderingMode mode)
        {
            switch (mode)
            {
                case PdfTextRenderingMode.Stroke:
                case PdfTextRenderingMode.FillAndStroke:
                case PdfTextRenderingMode.StrokeAndClip:
                case PdfTextRenderingMode.FillAndStrokeAndClip:
                    return true;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool ShouldClip(PdfTextRenderingMode mode)
        {
            switch (mode)
            {
                case PdfTextRenderingMode.Clip:
                case PdfTextRenderingMode.FillAndClip:
                case PdfTextRenderingMode.StrokeAndClip:
                case PdfTextRenderingMode.FillAndStrokeAndClip:
                    return true;
                default:
                    return false;
            }
        }
    }
}