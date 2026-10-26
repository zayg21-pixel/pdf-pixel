using PdfPixel.Geometry;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Visible page of a <see cref="PdfPanelFrame"/>.
/// </summary>
public readonly struct PdfPanelFramePage
{
    private readonly PdfPanelPage _page;
    private readonly PdfMatrix _contentToPanel;

    internal PdfPanelFramePage(
        in RequestPageInfo page,
        float scale,
        PdfPanelRenderedLayerInfo? renderedContent,
        PdfPanelRenderedLayerInfo? renderedAnnotations)
    {
        _page = page.Page;
        Rotation = page.Rotation;
        RenderedContent = renderedContent;
        RenderedAnnotations = renderedAnnotations;
        _contentToPanel = page.GetContentToPanelMatrix(scale);
    }

    /// <summary>
    /// Gets the page number.
    /// </summary>
    public int PageNumber => _page.PageNumber;

    /// <summary>
    /// Human-readable page label (e.g. "iv", "1", "A-1") as defined in the PDF.
    /// </summary>
    public string Label => _page.Label;

    /// <summary>
    /// Size of the page without rotation, in panel units.
    /// </summary>
    public PdfSize Size => _page.Size;

    /// <summary>
    /// Clockwise rotation of the page in degrees, normalized to 0-359.
    /// </summary>
    public int Rotation { get; }

    /// <summary>
    /// Gets the information about the rendered page content, or <see langword="null"/> if it has not been rendered yet.
    /// </summary>
    public PdfPanelRenderedLayerInfo? RenderedContent { get; }

    /// <summary>
    /// Gets the information about the rendered page annotations, or <see langword="null"/> if they have not been rendered yet.
    /// </summary>
    public PdfPanelRenderedLayerInfo? RenderedAnnotations { get; }

    /// <summary>
    /// Gets the rotated size of the page.
    /// </summary>
    public PdfSize RotatedSize => _page.GetRotatedSize(Rotation);

    /// <summary>
    /// Returns the matrix that maps panel pixels to page space rotated by <paramref name="rotation"/> degrees,
    /// with the origin at the top-left corner of the rotated page.
    /// </summary>
    /// <param name="rotation">Rotation of the page space relative to the unrotated page content, a multiple of 90.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="rotation"/> is not a multiple of 90.</exception>
    public PdfMatrix GetPanelToPage(int rotation)
    {
        int normalizedRotation = rotation % 360;

        if (normalizedRotation < 0)
        {
            normalizedRotation += 360;
        }

        if (normalizedRotation % 90 != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rotation));
        }

        float width = _page.Size.Width;
        float height = _page.Size.Height;
        float rotatedWidth = (normalizedRotation % 180 == 0) ? width : height;
        float rotatedHeight = (normalizedRotation % 180 == 0) ? height : width;
        float rotationOffsetX = normalizedRotation switch { 90 or 180 => rotatedWidth, _ => 0f };
        float rotationOffsetY = normalizedRotation switch { 180 or 270 => rotatedHeight, _ => 0f };

        PdfMatrix contentToPage = PdfMatrix.CreateRotationDegrees(normalizedRotation)
            .PostConcat(PdfMatrix.CreateTranslation(rotationOffsetX, rotationOffsetY));

        return _contentToPanel.Invert().PostConcat(contentToPage);
    }
}
