using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Web.Emscripten;
using System;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// <see cref="IPdfCancellableExecutionObserver"/> backed by a per-request SharedArrayBuffer.
/// Created on the worker thread, reads/writes via the worker-side SAB map keyed by request ID.
/// The main thread holds the same underlying SAB in its own map and can cancel the request
/// from the other side by setting the flag atomically.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class SharedArrayBufferExecutionObserver : IPdfCancellableExecutionObserver
{
    private readonly string _requestId;
    private readonly int _flagType;

    internal SharedArrayBufferExecutionObserver(string requestId, CancelFlagType flagType)
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
