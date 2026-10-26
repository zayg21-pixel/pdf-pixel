using PdfPixel.Geometry;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Layout of a presented panel image: its size, the mapping from host coordinates and the visible pages.
/// </summary>
public sealed class PdfPanelFrame
{
    internal PdfPanelFrame(DrawingRequest request, in PdfMatrix hostToPanel, PdfPanelDocument panelDocument)
    {
        HostToPanel = hostToPanel;
        PanelSize = request.PanelSize;

        var pages = new PdfPanelFramePage[request.Pages.Length];

        for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            RequestPageInfo page = request.Pages[pageIndex];

            pages[pageIndex] = new PdfPanelFramePage(
                page,
                request.Scale,
                panelDocument.GetRenderedContentInfo(page.PageNumber),
                panelDocument.GetRenderedAnnotationsInfo(page.PageNumber));
        }

        Pages = pages;
    }

    /// <summary>
    /// Matrix that maps host coordinates to panel pixels.
    /// </summary>
    public PdfMatrix HostToPanel { get; }

    /// <summary>
    /// Width and height of the panel in device pixels.
    /// </summary>
    public PdfSize PanelSize { get; }

    /// <summary>
    /// Pages visible in the panel, in render order.
    /// </summary>
    public IReadOnlyList<PdfPanelFramePage> Pages { get; }
}
