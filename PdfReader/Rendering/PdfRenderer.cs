using PdfReader.Models;
using PdfReader.Rendering.Text;
using PdfReader.Rendering.Path;
using PdfReader.Rendering.Image;
using PdfReader.Rendering.Shading;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Management;
using PdfReader.Fonts.Types;
using PdfReader.Imaging.Model;
using PdfReader.Text;
using PdfReader.Shading.Model;
using PdfReader.Color.Paint;
using PdfReader.Rendering.State;

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
            return _textRenderer.DrawText(canvas, ref pdfText, page, state, font);
        }

        /// <summary>
        /// Draw text with positioning adjustments (TJ operator) and return total advancement
        /// Applies Y-flip only around glyph rendering, preserving correct transformation order
        /// Updated to use PdfFontBase hierarchy
        /// </summary>
        public float DrawTextWithPositioning(SKCanvas canvas, IPdfValue arrayOperand, PdfPage page, PdfGraphicsState state, PdfFontBase font)
        {
            return _textRenderer.DrawTextWithPositioning(canvas, arrayOperand, page, state, font);
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
            // TODO: move scaling logic into image drawer to allow more flexible usage
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