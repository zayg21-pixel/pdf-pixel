using Microsoft.Extensions.Logging;
using PdfReader.Color.Paint;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Types;
using PdfReader.Rendering.State;
using PdfReader.Text;
using PdfReader.Transparency.Utilities;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Manages text drawing with proper selection and positioning.
    /// </summary>
    public class PdfTextRenderer : IPdfTextRenderer
    {
        private readonly IPdfRenderer _renderer;
        private readonly ILogger<PdfTextRenderer> _logger;

        internal PdfTextRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
        {
            _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }
            
            _logger = loggerFactory.CreateLogger<PdfTextRenderer>();
        }

        /// <summary>
        /// Draw text with positioning adjustments (TJ operator) and return total advancement
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public float DrawTextSequence(SKCanvas canvas, PdfTextSequence textSequence, PdfGraphicsState state, PdfFontBase font)
        {
            if (textSequence.Items.Count == 0)
            {
                return 0;
            }

            var shapedGlyphs = new List<ShapedGlyph>();

            for (int i = 0; i < textSequence.Items.Count; i++)
            {
                var item = textSequence.Items[i];

                switch (item.Kind)
                {
                    case PdfTextPositioningKind.Text:
                    {
                        var text = item.Text;
                        var glyphs = ShapeText(ref text, state, font);
                        shapedGlyphs.AddRange(glyphs);

                        break;
                    }
                    case PdfTextPositioningKind.Adjustment:
                    {
                        var adjustment = item.Adjustment;
                        var adjustmentInUserSpace = -adjustment / 1000f;

                        if (shapedGlyphs.Count > 0)
                        {
                            // Add advance to last glyph
                            var last = shapedGlyphs[shapedGlyphs.Count - 1];
                            shapedGlyphs[shapedGlyphs.Count - 1] = new ShapedGlyph(last.GlyphId, last.Width, last.AdvanceAfter + adjustmentInUserSpace);
                        }
                        else
                        {
                            shapedGlyphs.Insert(0, new ShapedGlyph(0, 0, adjustmentInUserSpace));
                        }

                        break;
                    }
                }
            }

            float width = 0f;

            if (shapedGlyphs.Count > 0)
            {
                using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, state);

                softMaskScope.BeginDrawContent();

                using var skFont = font.GetSkiaFont();

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

            if (ShouldFill(state.TextRenderingMode))
            {
                using var textFillTarget = new TextFillRenderTarget(font, shapingResult, state);
                textFillTarget.Render(canvas);
            }

            if (ShouldStroke(state.TextRenderingMode))
            {
                using var textStrokeTarget = new TextStrokeRenderTarget(font, shapingResult, state);
                textStrokeTarget.Render(canvas);
            }

            // Apply clipping if requested (modes with Clip). Pure clip mode skips drawing above.
            if (ShouldClip(state.TextRenderingMode))
            {
                using var textPath = TextRenderUtilities.GetTextPath(shapingResult, font, state);
                if (!textPath.IsEmpty)
                {
                    state.TextClipPath ??= new SKPath();
                    state.TextClipPath.AddPath(textPath);
                }
            }

            return TextRenderUtilities.GetTextWidth(shapingResult) * fullHorizontalScale;
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