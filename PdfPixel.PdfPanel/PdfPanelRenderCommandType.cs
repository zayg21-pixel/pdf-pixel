namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines the type of render command to execute.
/// Commands are executed in order to progressively render pages.
/// </summary>
public enum PdfPanelRenderCommandType
{
    /// <summary>
    /// Draw background color and page shadows.
    /// </summary>
    DrawBackground,

    /// <summary>
    /// Invalidates page and generates page thumbnail.
    /// </summary>
    InitializePage,

    /// <summary>
    /// Draw an already-generated thumbnail to the surface.
    /// </summary>
    DrawThumbnail,

    /// <summary>
    /// Generate full content picture for a specific page (CPU/GPU work, slow).
    /// </summary>
    GenerateContent,

    /// <summary>
    /// Draw already-generated content to the surface.
    /// </summary>
    DrawContent,

    /// <summary>
    /// Flush and present the current surface to screen.
    /// </summary>
    Render,

    /// <summary>
    /// Diposes all resources associated with the rendering process. Should be called when rendering is no longer needed (e.g. panel is closed).
    /// </summary>
    Dispose,

    /// <summary>
    /// Resets rendering view, can be used to clear the surface and cancel ongoing rendering without disposing resources.
    /// Should be followed by new drawing commands if rendering is to continue.
    /// </summary>
    Reset,
}
