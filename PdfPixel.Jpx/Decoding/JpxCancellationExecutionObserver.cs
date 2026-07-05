using System.Threading;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Calls <see cref="CancellationToken.ThrowIfCancellationRequested"/> on notify.
/// </summary>
public sealed class JpxCancellationExecutionObserver : IJpxExectionObserver
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    /// <summary>
    /// Initializes the observer with the cancellation token source to check on each notification.
    /// </summary>
    public JpxCancellationExecutionObserver(CancellationTokenSource cancellationTokenSource) => _cancellationTokenSource = cancellationTokenSource;

    /// <inheritdoc/>
    public void Notify() => _cancellationTokenSource.Token.ThrowIfCancellationRequested();
}
