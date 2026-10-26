using PdfPixel.Color;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Thumbnails;
using System;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Rendering request that includes page layout and visual parameters.
/// </summary>
internal sealed class PagesDrawingRequest : DrawingRequest
{
    /// <summary>
    /// Whether the pages are the ones ahead of the visible pages in the scroll direction, decoded but not drawn.
    /// </summary>
    public bool IsLookAhead { get; set; }

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
    /// Corner radius for page rendering in unscaled panel space.
    /// </summary>
    public float PageCornerRadius { get; set; }

    /// <summary>
    /// Draws an animated placeholder over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation { get; set; }

    /// <summary>
    /// Pages whose thumbnails are drawn in place of pages that have no decoded content yet.
    /// </summary>
    public PdfPanelThumbnailPlaceholderMode ThumbnailPlaceholderMode { get; set; }

    /// <summary>
    /// ON/OFF state of the optional content groups content is rendered with, or null for the document's default configuration.
    /// </summary>
    public PdfOptionalContentStates? OptionalContentStates { get; set; }

    /// <summary>
    /// Returns whether the page with the given page number decoded for this request also satisfies <paramref name="other"/>:
    /// the same scale, drawing and optional content settings and active annotation, and a region of interest containing the one of <paramref name="other"/>.
    /// </summary>
    public bool CoversPage(int pageNumber, PagesDrawingRequest other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        return Scale == other.Scale
            && RequestPageInfo.CoversRegion(GetPage(pageNumber).RegionOfInterest, other.GetPage(pageNumber).RegionOfInterest)
            && Antialias == other.Antialias
            && SnapToDevicePixels == other.SnapToDevicePixels
            && ActiveAnnotation == other.ActiveAnnotation
            && ActiveAnnotationState == other.ActiveAnnotationState
            && !other.ForceAnnotationUpdate
            && OptionalContentStates == other.OptionalContentStates;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj is PagesDrawingRequest other)
        {
            return base.Equals(obj)
                && IsLookAhead == other.IsLookAhead
                && Antialias == other.Antialias
                && SnapToDevicePixels == other.SnapToDevicePixels
                && BackgroundColor.Equals(other.BackgroundColor)
                && PageCornerRadius == other.PageCornerRadius
                && ShowPageLoadingAnimation == other.ShowPageLoadingAnimation
                && ThumbnailPlaceholderMode == other.ThumbnailPlaceholderMode
                && OptionalContentStates == other.OptionalContentStates;
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
        hash.Add(IsLookAhead);
        hash.Add(Antialias);
        hash.Add(SnapToDevicePixels);
        hash.Add(BackgroundColor);
        hash.Add(PageCornerRadius);
        hash.Add(ShowPageLoadingAnimation);
        hash.Add(ThumbnailPlaceholderMode);
        return hash.ToHashCode();
    }
}
