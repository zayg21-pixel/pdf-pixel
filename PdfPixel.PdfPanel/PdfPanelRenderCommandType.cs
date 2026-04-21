namespace PdfPixel.PdfPanel;

/// <summary>
/// Defines the type of render command to execute.
/// Commands are executed in order to progressively render pages.
/// </summary>
public enum PdfPanelRenderCommandType
{
    /// <summary>
    /// Initialize rendering.
    /// </summary>
    Initialize,

    /// <summary>
    /// Draw background color and page shadows.
    /// </summary>
    DrawBackground,

    /// <summary>
    /// Invalidates page and generates page thumbnail.
    /// </summary>
    InitializePage,

    /// <summary>
    /// Rasterize a thumbnail picture to the surface.
    /// </summary>
    RasterizeThumbnail,

    /// <summary>
    /// Draw an already-generated thumbnail to the surface.
    /// </summary>
    DrawThumbnail,

    /// <summary>
    /// Generate full content picture for a specific page.
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

    /// <summary>
    /// Captures a snapshot of the fully rendered surface for use as a backdrop in the next render pass.
    /// Emitted as the last command in a successful render pack. If the pack is cancelled, this command
    /// is never reached, so no stale or partial snapshot is stored.
    /// </summary>
    Finalize
}
