using PdfPixel.Color;
using System;

namespace PdfPixel.PdfPanel.Rendering;

public sealed partial class PdfPanelRenderer
{
    internal const float DefaultPageCornerRadius = 0f;
    internal const bool DefaultShowPageLoadingAnimation = true;
    internal const bool DefaultAntialias = true;
    internal const bool DefaultSnapToDevicePixels = true;
    internal const int DefaultTileSize = 512;
    internal const int DefaultAnimationFps = 60;
    internal const int DefaultLookAheadPageCount = 2;

    internal static readonly PdfColor DefaultBackgroundColor = PdfColors.LightGray;
    internal static readonly TimeSpan DefaultScrollContentUpdateDelay = TimeSpan.Zero;
    internal static readonly TimeSpan DefaultZoomContentUpdateDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public PdfColor BackgroundColor { get; set; } = DefaultBackgroundColor;

    /// <summary>
    /// Corner radius for page rendering in unscaled panel space.
    /// A value of 0 renders pages with sharp corners.
    /// </summary>
    public float PageCornerRadius { get; set; } = DefaultPageCornerRadius;

    /// <summary>
    /// Draws an animated placeholder over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation { get; set; } = DefaultShowPageLoadingAnimation;

    /// <summary>
    /// If true - antialiasing is enabled for page content.
    /// </summary>
    public bool Antialias { get; set; } = DefaultAntialias;

    /// <summary>
    /// If true - rects and image tiles are snapped to whole device pixels.
    /// </summary>
    public bool SnapToDevicePixels { get; set; } = DefaultSnapToDevicePixels;

    /// <summary>
    /// Edge length of a single content tile in device pixels.
    /// </summary>
    public int TileSize { get; set; } = DefaultTileSize;

    /// <summary>
    /// Frames per second the renderer drives its animations at.
    /// </summary>
    public int AnimationFps { get; set; } = DefaultAnimationFps;

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
}

