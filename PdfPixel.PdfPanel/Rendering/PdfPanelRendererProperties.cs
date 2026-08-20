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
    /// Edge length of a single content tile in device pixels.
    /// </summary>
    public int TileSize { get; set; } = 1024;

    /// <summary>
    /// Time a request that brings no new page into view waits before background decoding starts.
    /// Each further such request restarts the wait. <see cref="TimeSpan.Zero"/> starts decoding on every request.
    /// </summary>
    public TimeSpan ContentUpdateDelay { get; set; } = TimeSpan.FromMilliseconds(100);
}
