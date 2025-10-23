using PdfReader.Models;
using PdfReader.Rendering.Text;
using PdfReader.Rendering.Path;
using PdfReader.Rendering.Image;
using PdfReader.Rendering.Shading;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Types;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Central coordinator for PDF rendering operations with clean delegation
    /// Acts as a facade for the various specialized rendering components
    /// Updated to use PdfFontBase hierarchy
    /// </summary>
    public class PdfRenderer
    {
        private readonly IPdfTextDrawer _textRenderer;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        private readonly IPathDrawer _pathRenderer = new StandardPathDrawer();
        private readonly IImageDrawer _imageRenderer;
        private readonly IShadingDrawer _shadingRenderer;

        internal PdfRenderer(IFontCache fontCache, ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<PdfRenderer>();
            _imageRenderer = new FastImageDrawer(_loggerFactory);
            _textRenderer = new PdfTextDrawer(fontCache, _loggerFactory);
            _shadingRenderer = new StandardShadingDrawer(_loggerFactory);
        }

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
        /// Draw an image in PDF unit coordinate space (0,0) to (1,1) with proper transformations.
        /// This is a convenience method for XObject image processing.
        /// Handles coordinate transformation for proper image positioning.
        /// </summary>
        public void DrawUnitImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state, PdfPage page)
        {
            canvas.Save();
            canvas.Scale(1, -1);
            var destRect = new SKRect(0, -1, 1, 0);
            _imageRenderer.DrawImage(canvas, pdfImage, state, page, destRect);
            canvas.Restore();
        }

        /// <summary>
        /// Draw a shading fill described by the shading dictionary (operator 'sh').
        /// Soft mask application is delegated to the shading drawer implementation.
        /// Caller must apply the appropriate CTM prior to invocation.
        /// </summary>
        /// <param name="canvas">Destination canvas to draw on.</param>
        /// <param name="shading">Shading object defining the gradient/pattern.</param>
        /// <param name="state">Current graphics state providing CTM and soft mask.</param>
        /// <param name="page">Page context for resource access and color space resolution.</param>
        public void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state, PdfPage page)
        {
            _shadingRenderer.DrawShading(canvas, shading, state, page);
        }
    }
}