using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Data for <see cref="PdfPageContentProvider.ExtractedContentCleared"/>.
/// </summary>
internal sealed class ExtractedContentClearedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the arguments for the given cleared content types.
    /// </summary>
    public ExtractedContentClearedEventArgs(ExtractedContentType contentTypes) => ContentTypes = contentTypes;

    /// <summary>
    /// Content types that were cleared.
    /// </summary>
    public ExtractedContentType ContentTypes { get; }
}
