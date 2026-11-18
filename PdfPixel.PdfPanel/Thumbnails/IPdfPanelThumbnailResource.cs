using System;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// The host's pixels of a page thumbnail (e.g. a bitmap), created by <see cref="IPdfPanelThumbnailResourceFactory"/>
/// and owned by <see cref="PdfPanelThumbnails"/>, which disposes it when the thumbnail is evicted or cleared.
/// Disposed on the UI thread.
/// </summary>
public interface IPdfPanelThumbnailResource : IDisposable
{
}
