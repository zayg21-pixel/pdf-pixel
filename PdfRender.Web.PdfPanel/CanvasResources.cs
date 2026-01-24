using PdfRender.Canvas;

namespace PdfRender.Web.PdfPanel
{
    /// <summary>
    /// Encapsulates all resources associated with a single PDF viewer canvas instance.
    /// </summary>
    internal class CanvasResources
    {
        /// <summary>
        /// Gets or sets the render target factory for the canvas.
        /// </summary>
        public ICanvasRenderTargetFactory RenderTargetFactory { get; set; }

        /// <summary>
        /// Gets or sets the rendering queue for the canvas.
        /// </summary>
        public PdfRenderingQueue RenderingQueue { get; set; }

        /// <summary>
        /// Gets or sets the Skia surface factory for the canvas.
        /// </summary>
        public ISkSurfaceFactory SkSurfaceFactory { get; set; }

        /// <summary>
        /// Gets or sets the PDF viewer canvas instance.
        /// </summary>
        public PdfViewerCanvas ViewerCanvas { get; set; }

        /// <summary>
        /// Gets or sets the parsed configuration for the canvas.
        /// </summary>
        public PdfPanelConfiguration Configuration { get; set; }
    }
}