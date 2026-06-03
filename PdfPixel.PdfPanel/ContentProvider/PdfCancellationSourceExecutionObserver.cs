using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="IPdfCancellableExecutionObserver"/> backed by a <see cref="System.Threading.CancellationTokenSource"/>.
/// </summary>
public sealed class PdfCancellationSourceExecutionObserver : IPdfCancellableExecutionObserver
{
    /// <summary>
    /// The cancellation source that controls this observer.
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    /// <inheritdoc/>
    public void Notify()
    {
        try
        {
            CancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
        catch (ObjectDisposedException)
        {
            throw new OperationCanceledException();
        }
    }

    /// <inheritdoc/>
    public void Cancel()
    {
        try
        {
            if (!CancellationTokenSource.IsCancellationRequested)
            {
                CancellationTokenSource.Cancel();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Cancel();
        CancellationTokenSource.Dispose();
    }
}
