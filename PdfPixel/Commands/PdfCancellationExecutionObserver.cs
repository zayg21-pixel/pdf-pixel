using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Calls <see cref="CancellationToken.ThrowIfCancellationRequested"/> on notify.
/// </summary>
public sealed class PdfCancellationExecutionObserver : IPdfExecutionObserver
{
    private readonly CancellationToken _token;

    public PdfCancellationExecutionObserver(in CancellationToken token) => _token = token;

    public void Notify() => _token.ThrowIfCancellationRequested();
}
