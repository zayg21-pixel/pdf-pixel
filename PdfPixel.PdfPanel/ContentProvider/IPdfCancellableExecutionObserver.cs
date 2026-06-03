using PdfPixel.Commands;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// An <see cref="IPdfExecutionObserver"/> that can be cancelled and disposed.
/// Implementations are created by <see cref="IPdfPageContentProvider"/> and held by <see cref="PdfPageCacheEntry"/>.
/// </summary>
public interface IPdfCancellableExecutionObserver : IPdfExecutionObserver, IDisposable
{
    /// <summary>
    /// Signals cancellation without releasing resources.
    /// </summary>
    void Cancel();
}
