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
}
