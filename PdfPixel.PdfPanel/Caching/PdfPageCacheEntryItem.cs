using PdfPixel.Commands;
using PdfPixel.PdfPanel.Content;
using PdfPixel.Text;
using System;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// Represents page cache content item. Main content or annotation content.
/// </summary>
internal sealed class PdfPageCacheEntryItem : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Initializes the item for the given page number.
    /// </summary>
    public PdfPageCacheEntryItem(int pageNumber) => PageNumber = pageNumber;

    /// <summary>
    /// 1-based page number this item belongs to.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Represents page content as set of commands.
    /// </summary>
    public ContentLocker<PdfCommandRecorder> ContentCommandRecording { get; } = new();

    /// <summary>
    /// Words of the page in reading order.
    /// Kept when the content is cleared.
    /// </summary>
    public ContentLocker<PdfWord[]> Words { get; } = new();

    /// <summary>
    /// Content items of the page in drawing order.
    /// Kept when the content is cleared.
    /// </summary>
    public ContentLocker<PdfPanelContentItem[]> ContentItems { get; } = new();

    /// <summary>
    /// Replace page content with a new command recording. Disposes the previous recording if present.
    /// </summary>
    /// <param name="commandRecording">New command recording. May be null.</param>
    public void UpdateContentCommandRecording(PdfCommandRecorder? commandRecording)
    {
        ThrowIfDisposed();
        ContentCommandRecording.SetContent(commandRecording);
    }

    /// <summary>
    /// Clears the command recording. Extracted characters are kept.
    /// </summary>
    public void ClearContent()
    {
        ThrowIfDisposed();

        ContentCommandRecording.SetContent(default);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfPageCacheEntryItem));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearContent();
        _disposed = true;
    }
}
