using PdfPixel.Geometry;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Layout of a presented panel image: its size, the mapping from host coordinates and the visible pages.
/// </summary>
public sealed class PdfPanelFrame
{
    internal PdfPanelFrame(in PdfSize panelSize, in PdfMatrix hostToPanel, PdfPanelFramePage[] pages)
    {
        PanelSize = panelSize;
        HostToPanel = hostToPanel;
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
