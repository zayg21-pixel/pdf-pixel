using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Data for <see cref="PdfPanelTiles.TilesChanged"/>: the page whose tiles to draw changed.
/// </summary>
public sealed class PdfPanelTilesChangedEventArgs : EventArgs
{
    internal PdfPanelTilesChangedEventArgs(int pageNumber) => PageNumber = pageNumber;

    /// <summary>
    /// Gets the number of the page whose tiles changed.
    /// </summary>
    public int PageNumber { get; }
}
