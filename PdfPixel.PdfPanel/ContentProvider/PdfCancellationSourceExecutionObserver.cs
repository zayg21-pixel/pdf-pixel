using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="IPdfCancellableExecutionObserver"/> backed by a <see cref="System.Threading.CancellationTokenSource"/>.
/// </summary>
public abstract class PdfCancellationSourceExecutionObserver : IPdfCancellableExecutionObserver
{
    private bool _disposed;

    /// <summary>
    /// The cancellation source that controls this observer.
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    /// <inheritdoc/>
    public abstract ValueTask YieldAsync();

    /// <inheritdoc/>
    public void Notify()
    {
        if (_disposed)
        {
            throw new OperationCanceledException();
        }

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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Cancels the observer and releases its cancellation source.
    /// </summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        Cancel();
        CancellationTokenSource.Dispose();
        _disposed = true;
    }
}
