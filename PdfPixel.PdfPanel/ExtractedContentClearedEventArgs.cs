using PdfPixel.PdfPanel.Caching;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Data for <see cref="PdfPanelDocument.ExtractedContentCleared"/>.
/// </summary>
internal sealed class ExtractedContentClearedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the arguments for the given cleared content types.
    /// </summary>
    public ExtractedContentClearedEventArgs(CachedContentType contentTypes) => ContentTypes = contentTypes;

    /// <summary>
    /// Content types that were cleared.
    /// </summary>
    public CachedContentType ContentTypes { get; }
}
