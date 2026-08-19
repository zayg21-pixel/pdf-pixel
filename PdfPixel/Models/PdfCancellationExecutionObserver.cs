using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.Models;

/// <summary>
/// Calls <see cref="CancellationToken.ThrowIfCancellationRequested"/> on notify.
/// </summary>
public sealed class PdfCancellationExecutionObserver : IPdfExecutionObserver
{
    private readonly CancellationToken _token;

    /// <summary>
    /// Initializes the observer with the cancellation token to check on each notification.
    /// </summary>
#pragma warning disable RCS1231 // Make parameter ref read-only
    public PdfCancellationExecutionObserver(CancellationToken token) => _token = token;
#pragma warning restore RCS1231 // Make parameter ref read-only

    /// <inheritdoc/>
    public void Notify() => _token.ThrowIfCancellationRequested();

    /// <inheritdoc/>
    public async ValueTask YieldAsync() => await Task.Yield();
}
