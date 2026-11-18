using PdfPixel.Color;
using PdfPixel.Snap;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Appearance and rendering quality of the panel: the settings the host draws the pages with and the content tiles
/// are produced with.
/// </summary>
public sealed class PdfPanelRenderer
{
    internal const float DefaultPageCornerRadius = 0f;
    internal const bool DefaultAntialias = true;
    internal const int DefaultTileSize = 512;
    internal const int DefaultLookAheadPageCount = 1;
    internal const PdfPanelTileChannelOrder DefaultTileChannelOrder = PdfPanelTileChannelOrder.Rgba;

    internal static readonly PdfRgbaColor DefaultBackgroundColor = new(211f / 255f, 211f / 255f, 211f / 255f);
    internal static readonly TimeSpan DefaultScrollContentUpdateDelay = TimeSpan.Zero;
    internal static readonly TimeSpan DefaultZoomContentUpdateDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public PdfRgbaColor BackgroundColor { get; set; } = DefaultBackgroundColor;

    /// <summary>
    /// Corner radius for page rendering in unscaled panel space.
    /// A value of 0 renders pages with sharp corners.
    /// </summary>
    public float PageCornerRadius { get; set; } = DefaultPageCornerRadius;

    /// <summary>
    /// If true - antialiasing is enabled for page content.
    /// </summary>
    public bool Antialias { get; set; } = DefaultAntialias;

    /// <summary>
    /// Edge length of a single content tile in device pixels.
    /// </summary>
    public int TileSize { get; set; } = DefaultTileSize;

    /// <summary>
    /// Order of the channels of the content tiles' pixels, the order the host shows them in.
    /// </summary>
    public PdfPanelTileChannelOrder TileChannelOrder { get; set; } = DefaultTileChannelOrder;

    /// <summary>
    /// Time a scrolled request waits before the content of the pages that stayed visible is decoded again.
    /// A page that comes into view is decoded at once.
    /// </summary>
    public TimeSpan ScrollContentUpdateDelay { get; set; } = DefaultScrollContentUpdateDelay;

    /// <summary>
    /// Time a zoomed request waits before the content of the pages that stayed visible is decoded again.
    /// A page that comes into view is decoded at once.
    /// </summary>
    public TimeSpan ZoomContentUpdateDelay { get; set; } = DefaultZoomContentUpdateDelay;

    /// <summary>
    /// Number of pages ahead of the visible pages in the scroll direction whose content is decoded after them.
    /// </summary>
    public int LookAheadPageCount { get; set; } = DefaultLookAheadPageCount;

    // TODO: no frame surface is drawn anymore; OnPanelPageRendered and OnPanelRendered are not called until the host
    // draws overlays natively.

    /// <summary>
    /// Gets or sets the action that draws over a page each time the page is drawn, onto the frame surface in the page's
    /// content coordinates (top-left origin, unrotated), clipped to the page; it draws over the page content and the
    /// panel's own page graphics. It only draws: it must not reset the surface, change its context or read its pixels.
    /// </summary>
    public Action<PdfSnapSurface, PdfPanelFramePage>? OnPanelPageRendered { get; set; }

    /// <summary>
    /// Gets or sets the action that draws over the panel each time a part of it is drawn, onto the frame surface in
    /// panel pixels, clipped to the part drawn: the whole panel, or one page when only that page is drawn again. It
    /// only draws: it must not reset the surface, change its context or read its pixels.
    /// </summary>
    public Action<PdfSnapSurface, PdfPanelFrame>? OnPanelRendered { get; set; }
}

