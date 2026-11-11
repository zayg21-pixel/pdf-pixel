using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
using PdfPixel.PdfPanel.Thumbnails;
using System.Linq;

namespace PdfPixel.PdfPanel.Rendering;

// TODO: clean up the rest of the drawing request: no annotation and optional content states or antialias for the frame.

/// <summary>
/// Rendering request sent to <see cref="PdfPanelRenderer"/>: the view, the pages and the drawing settings.
/// </summary>
internal sealed class PagesDrawingRequest
{
    /// <summary>
    /// Current zoom scale factor.
    /// </summary>
    public float Scale { get; set; }

    /// <summary>
    /// Scale at which the pages are displayed at a zoom of 100%.
    /// </summary>
    public float ReferenceScale { get; set; }

    /// <summary>
    /// Horizontal and vertical scroll offset in scaled panel space.
    /// </summary>
    public PdfPoint Offset { get; set; }

    /// <summary>
    /// Width and height of the drawing surface in device pixels.
    /// </summary>
    public PdfSize PanelSize { get; set; }

    /// <summary>
    /// Target that receives the rendered surface for display.
    /// </summary>
    public IPdfPanelRenderTarget? RenderTarget { get; set; }

    /// <summary>
    /// Pages of the request, in render order.
    /// </summary>
    public RequestPageInfo[] Pages { get; set; } = [];

    /// <summary>
    /// If true - antialiasing is enabled for page content.
    /// </summary>
    public bool Antialias { get; set; }

    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public PdfRgbaColor BackgroundColor { get; set; }

    /// <summary>
    /// Corner radius for page rendering in unscaled panel space.
    /// </summary>
    public float PageCornerRadius { get; set; }

    /// <summary>
    /// Pages whose thumbnails are drawn in place of pages that have no decoded content yet.
    /// </summary>
    public PdfPanelThumbnailPlaceholderMode ThumbnailPlaceholderMode { get; set; }

    /// <summary>
    /// ON/OFF state of the optional content groups content is rendered with, or null for the document's default configuration.
    /// </summary>
    public PdfOptionalContentStates? OptionalContentStates { get; set; }

    /// <summary>
    /// States the annotations of the pages are drawn in.
    /// </summary>
    public PdfAnnotationStates AnnotationStates { get; set; } = PdfAnnotationStates.Empty;

    /// <summary>
    /// Returns the page of the request with the given page number.
    /// </summary>
    public RequestPageInfo GetPage(int pageNumber) => Pages.First(page => page.PageNumber == pageNumber);
}
