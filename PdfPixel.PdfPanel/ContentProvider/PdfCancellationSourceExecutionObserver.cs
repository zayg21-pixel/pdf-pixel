using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="IPdfCancellableExecutionObserver"/> backed by a <see cref="System.Threading.CancellationTokenSource"/>.
/// </summary>
public sealed class PdfCancellationSourceExecutionObserver : IPdfCancellableExecutionObserver
{
    private bool _disposed;

    /// <summary>
    /// The cancellation source that controls this observer.
    /// </summary>
    public CancellationTokenSource CancellationTokenSource { get; } = new();

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
    public async ValueTask YieldAsync() => await Task.Yield();

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
        _disposed = true;
    }
}
