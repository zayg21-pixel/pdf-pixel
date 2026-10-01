using PdfPixel.Color;
using System;

namespace PdfPixel.PdfPanel.Rendering;

public sealed partial class PdfPanelRenderer
{
    /// <summary>
    /// Background color drawn behind the pages.
    /// </summary>
    public PdfColor BackgroundColor { get; set; } = PdfColors.LightGray;

    /// <summary>
    /// Corner radius for page rendering in unscaled page space.
    /// A value of 0 renders pages with sharp corners.
    /// </summary>
    public float PageCornerRadius { get; set; }

    /// <summary>
    /// Draws an animated placeholder over pages that have no decoded content yet.
    /// </summary>
    public bool ShowPageLoadingAnimation { get; set; } = true;

    /// <summary>
    /// If true - antialiasing is enabled for page content.
    /// </summary>
    public bool Antialias { get; set; } = true;

    /// <summary>
    /// If true - rects and image tiles are snapped to whole device pixels.
    /// </summary>
    public bool SnapToDevicePixels { get; set; } = true;

    /// <summary>
    /// Edge length of a single content tile in device pixels.
    /// </summary>
    public int TileSize { get; set; } = 512;

    /// <summary>
    /// Frames per second the renderer drives its animations at.
    /// </summary>
    public int AnimationFps { get; set; } = 60;

    /// <summary>
    /// Time a scrolled request waits before page content decoding starts.
    /// </summary>
    public TimeSpan ScrollContentUpdateDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Time a zoomed request waits before page content decoding starts.
    /// </summary>
    public TimeSpan ZoomContentUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(200);
}
