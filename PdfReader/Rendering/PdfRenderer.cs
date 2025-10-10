using PdfReader.Fonts;
using PdfReader.Models;
using PdfReader.Rendering.Text;
using PdfReader.Rendering.Path;
using PdfReader.Rendering.Image;
using PdfReader.Rendering.Shading;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Management;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Central coordinator for PDF rendering operations with clean delegation
    /// Acts as a facade for the various specialized rendering components
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public class PdfRenderer
    {
        private readonly PdfTextRenderer _textRenderer;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private readonly StandardPathDrawer _pathRenderer = new StandardPathDrawer();
        private readonly FastImageDrawer _imageRenderer;
        private readonly IShadingDrawer _shadingRenderer;

        internal PdfRenderer(IFontCache fontCache, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<PdfRenderer>();
            _imageRenderer = new FastImageDrawer(_loggerFactory);
            _textRenderer = new PdfTextRenderer(fontCache, _loggerFactory);
            _shadingRenderer = new StandardShadingDrawer(_loggerFactory);
        }

        public IShadingDrawer ShadingDrawer => _shadingRenderer;

        /// <summary>
        /// Draw text with the current graphics state (updated to handle PdfText and PdfFontBase)
        /// Applies Y-flip only around glyph rendering, preserving correct transformation order
        /// </summary>
        public float DrawText(SKCanvas canvas, ref PdfText pdfText, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            if (pdfText.IsEmpty)
            {
                return 0f;
            }

            canvas.Save();

            // Apply text matrix transformation
            canvas.Concat(state.TextMatrix);

            // Apply text rise (vertical offset) - BEFORE Y-flip so direction is correct
            if (state.Rise != 0)
            {
                canvas.Translate(0, state.Rise);
            }

            // CRITICAL: Apply local Y-axis flip ONLY for glyph rendering
            // This preserves the correct transformation order while fixing glyph orientation
            canvas.Scale(1, -1);

            // Draw text and get advancement
            var advancement = _textRenderer.DrawText(canvas, ref pdfText, page, state, font);

            canvas.Restore();
            
            return advancement;
        }

        /// <summary>
        /// Draw text with positioning adjustments (TJ operator) and return total advancement
        /// Applies Y-flip only around glyph rendering, preserving correct transformation order
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public float DrawTextWithPositioning(SKCanvas canvas, IPdfValue arrayOperand, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            canvas.Save();
            
            // Apply text matrix transformation
            canvas.Concat(state.TextMatrix);
            
            // Apply text rise (vertical offset)
            if (state.Rise != 0)
            {
                canvas.Translate(0, state.Rise);
            }


            canvas.Scale(1, -1);
            
            // Delegate to text renderer for the actual text positioning logic and get advancement
            var totalAdvancement = _textRenderer.DrawTextWithPositioning(canvas, arrayOperand, page, state, font);
            
            canvas.Restore();
            
            return totalAdvancement;
        }

        /// <summary>
        /// Paint a path with the specified operation and fill type
        /// </summary>
        public void PaintPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, PdfPage page = null, SKPathFillType fillType = SKPathFillType.Winding)
        {
            _pathRenderer.DrawPath(canvas, path, state, operation, page, fillType);
        }

        /// <summary>
        /// Draw an image in PDF unit coordinate space (0,0) to (1,1) with proper transformations
        /// This is a convenience method for XObject image processing
        /// Handles coordinate transformation for proper image positioning
        /// </summary>
        public void DrawUnitImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state, PdfPage page)
        {
            // Save canvas state
            canvas.Save();

            // Apply local Y-axis flip to counteract global Y-flip for image content orientation
            canvas.Scale(1, -1);
            
            // For unit coordinate space, adjust the rectangle to work with the Y-flipped coordinate system
            var destRect = new SKRect(0, -1, 1, 0);  // Adjust for Y-flip in unit space
            
            // Draw the image using the standard image renderer
            _imageRenderer.DrawImage(canvas, pdfImage, state, page, destRect);

            canvas.Restore();
        }
    }
}