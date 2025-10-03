using PdfReader.Fonts;
using PdfReader.Models;
using SkiaSharp;

namespace PdfReader.Rendering.Text
{
    /// <summary>
    /// Manages text drawing with proper drawer selection and positioning
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public class PdfTextRenderer
    {
        private readonly ITextDrawer _textDrawer;

        internal PdfTextRenderer(IFontCache fontCache)
        {
            _textDrawer = new StandardTextDrawer(fontCache);
        }

        /// <summary>
        /// Draw text with the current graphics state and return advancement
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public float DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            if (pdfText.IsEmpty || font == null)
            {
                return 0f;
            }

            // Draw text and get advancement
            return _textDrawer.DrawText(canvas, ref pdfText, page, state, font);
        }

        /// <summary>
        /// Draw text with positioning adjustments (TJ operator) and return total advancement
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public float DrawTextWithPositioning(SKCanvas canvas, IPdfValue arrayOperand, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            if (arrayOperand.Type != PdfValueType.Array) return 0f;

            var array = arrayOperand.AsArray();
            if (array == null) return 0f;

            float totalAdvancement = 0f;

            foreach (var item in array)
            {
                if (item.Type == PdfValueType.String || item.Type == PdfValueType.HexString)
                {
                    // Use PdfText wrapper for proper CID font handling
                    var pdfText = PdfText.FromOperand(item);

                    if (!pdfText.IsEmpty)
                    {
                        // Draw text and get advancement
                        var advancement = DrawText(canvas, ref pdfText, page, state, font);

                        // Apply the advancement to move to next position
                        canvas.Translate(advancement, 0);
                        totalAdvancement += advancement;
                    }
                }
                else
                {
                    // Handle positioning adjustments (negative values in TJ array)
                    var adjustment = item.AsFloat();
                    // Convert from glyph units to user space units (PDF spec: thousandths of text unit)
                    var adjustmentInUserSpace = -adjustment * state.FontSize / 1000f;
                    canvas.Translate(adjustmentInUserSpace, 0);
                    totalAdvancement += adjustmentInUserSpace;
                }
            }

            return totalAdvancement;
        }
    }
}