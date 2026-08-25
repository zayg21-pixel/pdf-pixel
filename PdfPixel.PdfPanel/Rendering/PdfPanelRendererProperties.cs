using PdfPixel.Color;
using PdfPixel.PdfPanel.Text;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Configuration values a <see cref="PdfPanelRenderer"/> is created with.
/// </summary>
public sealed class PdfPanelRendererProperties
{
    /// <summary>
    /// Context that page and animation callbacks are posted to, or <see langword="null"/> to invoke them on the calling thread.
    /// </summary>
    public SynchronizationContext? SynchronizationContext { get; set; }

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
    /// Configuration the renderer's <see cref="PdfPanelTextSelector"/> is created with.
    /// </summary>
    public PdfPanelTextSelectorParameters TextSelectorParameters { get; set; } = new();

    /// <summary>
    /// Edge length of a single content tile in device pixels.
    /// </summary>
    public int TileSize { get; set; } = 1024;

    /// <summary>
    /// Time a request waits before page content decoding starts.
    /// </summary>
    public TimeSpan ContentUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(200);
}
