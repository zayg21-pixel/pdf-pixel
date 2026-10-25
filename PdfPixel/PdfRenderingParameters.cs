namespace PdfPixel;

/// <summary>
/// Parameters for PDF page rendering, passed to <see cref="IPdfPage.Render"/> and propagated through
/// <see cref="Rendering.PdfGraphicsState"/> for the lifetime of a single page parse.
/// </summary>
public class PdfRenderingParameters
{
    /// <summary>
    /// Tile size used when splitting large images into tiles during command recording.
    /// </summary>
    public int ImageTileSize { get; set; } = 256;

    /// <summary>
    /// When true, decoded tiles are retained across renders for the tiles inside the drawn region.
    /// When false, only the tile row currently being drawn is held.
    /// </summary>
    public bool CacheDecodedTiles { get; set; }

    /// <summary>
    /// Default number of samples for Postscript and Exponential functions when the point count is unknown.
    /// </summary>
    public int DefaultFunctionSamples { get; set; } = 64;

    /// <summary>
    /// Maximum number of tessellation vertices for mesh-based shadings.
    /// </summary>
    public int MaxTessellationVertices { get; set; } = 32;

    /// <summary>
    /// When true, stencil masks of at most <see cref="MaxStencilMaskSizeForPathConversion"/> samples are converted to
    /// paths and drawn as filled paths instead of images.
    /// </summary>
    public bool ConvertStencilMasksToPaths { get; set; } = true;

    /// <summary>
    /// Largest number of samples, width times height, of a stencil mask converted to a path.
    /// </summary>
    public int MaxStencilMaskSizeForPathConversion { get; set; } = 100000;

    /// <summary>
    /// When false, paths are neither rendered nor extracted.
    /// </summary>
    public bool ProcessPaths { get; set; } = true;

    /// <summary>
    /// When false, images are neither rendered nor extracted.
    /// </summary>
    public bool ProcessImages { get; set; } = true;

    /// <summary>
    /// When false, shadings are neither rendered nor extracted.
    /// </summary>
    public bool ProcessShadings { get; set; } = true;

    /// <summary>
    /// When false, text is neither rendered nor extracted.
    /// </summary>
    public bool ProcessText { get; set; } = true;

    /// <summary>
    /// When false, drawing commands of every processed content group are suppressed.
    /// </summary>
    public bool RenderContent { get; set; } = true;

    /// <summary>
    /// When true, an <see cref="Commands.IPdfContentCommand"/> is emitted for every painting operation of a processed
    /// content group and content scopes are resolved against the document structure tree.
    /// </summary>
    public bool ExtractContent { get; set; } = true;

    /// <summary>
    /// Returns a copy of these parameters with every content group, rendering and content extraction switched on.
    /// </summary>
    internal PdfRenderingParameters ForType3Rendering()
    {
        var parameters = (PdfRenderingParameters)MemberwiseClone();
        parameters.ProcessPaths = true;
        parameters.ProcessImages = true;
        parameters.ProcessShadings = true;
        parameters.ProcessText = true;
        parameters.RenderContent = true;
        parameters.ExtractContent = true;

        return parameters;
    }
}
