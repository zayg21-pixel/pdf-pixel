using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.Emscripten;
using System;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Worker-side <see cref="IPdfCancellableExecutionObserver"/> backed by a per-request SharedArrayBuffer.
/// Reads flags atomically to detect cancellation signalled by the main thread.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class WorkerSabExecutionObserver : IPdfCancellableExecutionObserver
{
    private readonly string _requestId;
    private readonly int _flagType;

    internal WorkerSabExecutionObserver(string requestId, CancelFlagType flagType)
    {
        _requestId = requestId;
        _flagType = (int)flagType;
    }

    /// <inheritdoc/>
    public void Notify()
    {
        if (EmscriptenInterop.WorkerReadRequestFlag(_requestId, _flagType) != 0)
        {
            throw new OperationCanceledException();
        }
    }

    /// <inheritdoc/>
    public void Cancel() => EmscriptenInterop.WorkerSetRequestFlag(_requestId, _flagType, 1);

    /// <inheritdoc/>
    public void Dispose() => Cancel();
}
