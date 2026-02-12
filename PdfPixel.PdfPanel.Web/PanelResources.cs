namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Encapsulates all resources associated with a single PDF panel instance.
/// </summary>
internal class PdfPanelResources
{
    /// <summary>
    /// Gets or sets the render target factory for the panel.
    /// </summary>
    public IPdfPanelRenderTargetFactory RenderTargetFactory { get; set; }

    /// <summary>
    /// Gets or sets the rendering queue for the panel.
    /// </summary>
    public PdfRenderingQueue RenderingQueue { get; set; }

    /// <summary>
    /// Gets or sets the Skia surface factory for the panel.
    /// </summary>
    public ISkSurfaceFactory SkSurfaceFactory { get; set; }

    /// <summary>
    /// Gets or sets the PDF panel context instance.
    /// </summary>
    public PdfPanelContext Context { get; set; }

    /// <summary>
    /// Gets or sets the parsed configuration for the panel.
    /// </summary>
    public PdfPanelConfiguration Configuration { get; set; }
}