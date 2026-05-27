using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Calls <see cref="CancellationToken.ThrowIfCancellationRequested"/> on notify.
/// </summary>
public sealed class PdfCancellationExecutionObserver : IPdfExecutionObserver
{
    private readonly CancellationToken _token;

#pragma warning disable RCS1231 // Make parameter ref read-only
    public PdfCancellationExecutionObserver(CancellationToken token) => _token = token;
#pragma warning restore RCS1231 // Make parameter ref read-only

    public void Notify() => _token.ThrowIfCancellationRequested();
}
