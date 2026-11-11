using PdfPixel.Annotations;
using PdfPixel.OptionalContent;

namespace PdfPixel.PdfPanel.Scheduling;

/// <summary>
/// Request sent to <see cref="PdfPanelScheduler"/> to produce the content of the pages of the panel.
/// </summary>
internal sealed class ContentUpdateRequest
{
    /// <summary>
    /// Initializes the request with the settings the tiles are rasterized with and its pages.
    /// </summary>
    public ContentUpdateRequest(
        float scale,
        int tileSize,
        int tileCapacity,
        bool antialias,
        PdfOptionalContentStates optionalContentStates,
        PdfAnnotationStates annotationStates,
        ContentUpdatePage[] pages)
    {
        Scale = scale;
        TileSize = tileSize;
        TileCapacity = tileCapacity;
        Antialias = antialias;
        OptionalContentStates = optionalContentStates;
        AnnotationStates = annotationStates;
        Pages = pages;
    }

    /// <summary>
    /// Scale the tiles are rasterized at.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Edge length of a tile in device pixels.
    /// </summary>
    public int TileSize { get; }

    /// <summary>
    /// Number of tiles the tile pool keeps, rented and free together.
    /// </summary>
    public int TileCapacity { get; }

    /// <summary>
    /// Whether the tiles are rasterized with antialiasing.
    /// </summary>
    public bool Antialias { get; }

    /// <summary>
    /// ON/OFF state of the optional content groups the content is rendered with.
    /// </summary>
    public PdfOptionalContentStates OptionalContentStates { get; }

    /// <summary>
    /// States the annotations are rasterized in.
    /// </summary>
    public PdfAnnotationStates AnnotationStates { get; }

    /// <summary>
    /// Pages of the request in the order they are produced: the visible pages, then the pages ahead of them.
    /// </summary>
    public ContentUpdatePage[] Pages { get; }
}
