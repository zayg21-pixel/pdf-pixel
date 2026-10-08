using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Information about the rendered picture of a page layer.
/// </summary>
public readonly struct PdfPanelRenderedLayerInfo
{
    internal PdfPanelRenderedLayerInfo(float scale, PdfRectangle? regionOfInterest)
    {
        Scale = scale;
        RegionOfInterest = regionOfInterest;
    }

    /// <summary>
    /// Gets the panel scale the layer was rendered at.
    /// </summary>
    public float Scale { get; }

    /// <summary>
    /// Gets the rendered part of the page, in content coordinates,
    /// or <see langword="null"/> when the whole page was rendered.
    /// </summary>
    public PdfRectangle? RegionOfInterest { get; }
}
