using PdfPixel.Geometry;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Layout of a presented panel image: its size, the mapping from host coordinates and the visible pages.
/// </summary>
public sealed class PdfPanelFrame
{
    internal PdfPanelFrame(DrawingRequest request, in PdfMatrix hostToPanel, PdfPageContentProvider contentProvider)
    {
        HostToPanel = hostToPanel;
        PanelSize = request.PanelSize;

        var pages = new PdfPanelFramePage[request.VisiblePages.Length];

        for (int pageIndex = 0; pageIndex < pages.Length; pageIndex++)
        {
            VisiblePageInfo page = request.VisiblePages[pageIndex];

            pages[pageIndex] = new PdfPanelFramePage(
                page,
                request.Scale,
                contentProvider.GetRenderedContentInfo(page.PageNumber),
                contentProvider.GetRenderedAnnotationsInfo(page.PageNumber));
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
