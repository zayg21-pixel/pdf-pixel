using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Rendering;

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
    /// Gets or sets the renderer for the panel.
    /// </summary>
    public PdfPanelRenderer Renderer { get; set; }

    /// <summary>
    /// Gets or sets the Skia surface factory for the panel.
    /// </summary>
    public ISkSurfaceFactory SkSurfaceFactory { get; set; }

    /// <summary>
    /// Gets or sets the PDF panel context instance.
    /// </summary>
    public PdfPanelContext Context { get; set; }

    /// <summary>
    /// Gets or sets the document currently loaded into the panel.
    /// </summary>
    public IPdfDocument Document { get; set; }

    /// <summary>
    /// Gets or sets the pages of the currently loaded document.
    /// </summary>
    public PdfPanelPageCollection Pages { get; set; }

    /// <summary>
    /// Gets or sets the parsed configuration for the panel.
    /// </summary>
    public PdfPanelConfiguration Configuration { get; set; }

    /// <summary>
    /// Gets or sets the last active annotation popup, used for click detection.
    /// </summary>
    public PdfAnnotationPopup LastAnnotationPopup { get; set; }

    /// <summary>
    /// Gets or sets the last annotation interaction state, used for click detection.
    /// </summary>
    public PdfPanelPointerState LastAnnotationState { get; set; }
}