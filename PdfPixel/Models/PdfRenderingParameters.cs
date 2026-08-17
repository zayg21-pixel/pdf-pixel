namespace PdfPixel.Models;

/// <summary>
/// Parameters for PDF page rendering, passed to <see cref="IPdfPage.Render"/> and propagated through
/// <see cref="PdfPixel.Rendering.State.PdfGraphicsState"/> for the lifetime of a single page parse.
/// </summary>
public class PdfRenderingParameters
{
    /// <summary>
    /// Tile size used when splitting large images into tiles during command recording.
    /// </summary>
    public int ImageTileSize { get; set; } = 512;

    /// <summary>
    /// Upper bound on the combined estimated byte size of cached decoded tiles.
    /// Each tile is estimated as Width * Height * 4 (RGBA8888).
    /// </summary>
    public long MaxTileCacheSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Default number of samples for Postscript and Exponential functions when the point count is unknown.
    /// </summary>
    public int DefaultFunctionSamples { get; set; } = 64;

    /// <summary>
    /// Maximum number of tessellation vertices for mesh-based shadings.
    /// </summary>
    public int MaxTessellationVertices { get; set; } = 32;

    /// <summary>
    /// When false, path drawing commands are suppressed.
    /// </summary>
    public bool RenderPaths { get; set; } = true;

    /// <summary>
    /// When false, image drawing commands are suppressed.
    /// </summary>
    public bool RenderImages { get; set; } = true;

    /// <summary>
    /// When false, shading drawing commands are suppressed.
    /// </summary>
    public bool RenderShadings { get; set; } = true;

    /// <summary>
    /// When false, text drawing commands are suppressed.
    /// </summary>
    public bool RenderText { get; set; } = true;

    /// <summary>
    /// When true, text character data is extracted and accumulated in the text block tree
    /// accessible via <see cref="Commands.PdfCommandExecutionContext.RootTextBlock"/> during command execution.
    /// </summary>
    public bool ExtractText { get; set; } = true;
}
