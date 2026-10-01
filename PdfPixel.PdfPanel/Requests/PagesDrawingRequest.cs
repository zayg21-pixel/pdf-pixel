using PdfPixel.Color;
using System;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Rendering request that includes page layout and visual parameters.
/// </summary>
internal sealed class PagesDrawingRequest : DrawingRequest
{
    /// <summary>
    /// If true - antialiasing is enabled for page content.
    /// </summary>
    public bool Antialias { get; set; }

    /// <summary>
    /// If true - rects and image tiles are snapped to whole device pixels.
    /// </summary>
    public bool SnapToDevicePixels { get; set; }

    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public PdfColor BackgroundColor { get; set; }

    /// <summary>
    /// Corner radius for page rendering in unscaled page space.
    /// </summary>
    public float PageCornerRadius { get; set; }

    /// <summary>
    /// Draws an animated placeholder over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation { get; set; }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is PagesDrawingRequest other)
        {
            return base.Equals(obj)
                && Antialias == other.Antialias
                && SnapToDevicePixels == other.SnapToDevicePixels
                && BackgroundColor.Equals(other.BackgroundColor)
                && PageCornerRadius == other.PageCornerRadius
                && ShowPageLoadingAnimation == other.ShowPageLoadingAnimation;
        }

        return false;
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        HashCode hash = new();

        hash.Add(base.GetHashCode());
        hash.Add(Scale);
        hash.Add(Offset);
        hash.Add(PanelSize);
        hash.Add(RenderTarget);
        hash.Add(ActiveAnnotation);
        hash.Add(ActiveAnnotationState);
        hash.Add(Antialias);
        hash.Add(SnapToDevicePixels);
        hash.Add(BackgroundColor);
        hash.Add(PageCornerRadius);
        hash.Add(ShowPageLoadingAnimation);
        return hash.ToHashCode();
    }
}
