using PdfPixel.Annotations;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Annotations;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// One square tile of a page on the tile grid of a <see cref="PdfPageTiles"/>: the host's pixels of it and the
/// settings it was rasterized with, empty until it is rasterized.
/// </summary>
internal sealed class PdfPageTile
{
    /// <summary>
    /// Initializes an empty tile that covers <paramref name="destination"/>.
    /// </summary>
    public PdfPageTile(in PdfRectangle destination) => Destination = destination;

    /// <summary>
    /// Gets the part of the page the whole tile covers, in content coordinates; it reaches past the page at its edges.
    /// </summary>
    public PdfRectangle Destination { get; }

    /// <summary>
    /// Gets whether the tile was rasterized.
    /// </summary>
    public bool IsRasterized => Resource != null;

    /// <summary>
    /// Gets the host's pixels of the tile, or <see langword="null"/> while it is empty.
    /// </summary>
    public IPdfPanelTileResource? Resource { get; private set; }

    /// <summary>
    /// Gets whether the tile was rasterized with antialiasing, or <see langword="null"/> while it is empty.
    /// </summary>
    public bool? Antialias { get; private set; }

    /// <summary>
    /// Gets the optional content states the tile was rasterized with, or <see langword="null"/> while it is empty.
    /// </summary>
    public PdfOptionalContentStates? OptionalContentStates { get; private set; }

    /// <summary>
    /// Gets the states the annotations of the tile were rasterized in, or <see langword="null"/> while it is empty.
    /// </summary>
    public PdfAnnotationStates? AnnotationStates { get; private set; }

    /// <summary>
    /// Returns whether the tile must be rasterized for <paramref name="antialias"/>,
    /// <paramref name="optionalContentStates"/> and <paramref name="annotationStates"/>: it is empty, was rasterized
    /// with another antialiasing or other optional content states, or covers an annotation of
    /// <paramref name="page"/> whose state differs.
    /// </summary>
    public bool IsOutOfDate(bool antialias, PdfOptionalContentStates optionalContentStates, PdfAnnotationStates annotationStates, PdfPanelPage page)
    {
        if (Antialias != antialias || OptionalContentStates != optionalContentStates || AnnotationStates == null)
        {
            return true;
        }

        foreach (PdfRectangle region in page.GetChangedAnnotationRegions(AnnotationStates, annotationStates))
        {
            PdfRectangle overlap = PdfRectangle.Intersect(Destination, region);

            if (overlap.Width > 0 && overlap.Height > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Records that the tile was rasterized into <paramref name="resource"/> with <paramref name="antialias"/>,
    /// <paramref name="optionalContentStates"/> and <paramref name="annotationStates"/>.
    /// </summary>
    public void SetRasterized(IPdfPanelTileResource resource, bool antialias, PdfOptionalContentStates optionalContentStates, PdfAnnotationStates annotationStates)
    {
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
        Antialias = antialias;
        OptionalContentStates = optionalContentStates ?? throw new ArgumentNullException(nameof(optionalContentStates));
        AnnotationStates = annotationStates ?? throw new ArgumentNullException(nameof(annotationStates));
    }
}
