using PdfPixel.PdfPanel.Requests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Default render loop runner that blocks on a semaphore waiting for work.
/// Uses a <see cref="ConcurrentQueue{T}"/> of <see cref="RequestEntry"/> items and a shared
/// <see cref="CancellationTokenSource"/> to batch-cancel pending skippable requests while
/// guaranteeing that non-skippable requests are never dropped.
/// </summary>
public sealed class DefaultRenderLoopRunner : IRenderLoopRunner
{
    private bool _disposed;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    private volatile bool _running;
    private Action<RenderFrameCommand> _iteration;

    /// <summary>
    /// Cross-thread queue: produced by <see cref="Enqueue"/>,
    /// consumed by the render loop in <see cref="Start"/>.
    /// </summary>
    private readonly ConcurrentQueue<RequestEntry> _requestQueue = new();

    /// <summary>
    /// Shared <see cref="CancellationTokenSource"/> for all pending skippable requests.
    /// Replaced each time a new request is enqueued, cancelling all previously queued skippable work.
    /// </summary>
    private CancellationTokenSource _skippableCts = new();

    /// <inheritdoc />
    public void Start(Action<RenderFrameCommand> iteration)
    {
        _iteration = iteration ?? throw new ArgumentNullException(nameof(iteration));
        _running = true;

        while (_running)
        {
            try
            {
                _semaphore.Wait();
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            if (!_running)
            {
                break;
            }

            ProcessQueue();
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        _running = false;
        _semaphore.Release();
    }

    /// <inheritdoc />
    public void Enqueue(DrawingRequest request)
    {
        if (_disposed)
        {
            return;
        }

        var commands = PdfPanelRenderCommand.GenerateCommandsFromRequest(request);
        var commandQueue = new Queue<PdfPanelRenderCommand>(commands);

        // Cancel all previously queued skippable requests.
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _skippableCts, newCts);
        oldCts.Cancel();
        oldCts.Dispose();

        // Non-skippable requests receive CancellationToken.None so they are never cancelled.
        var token = request.IsSkippable ? newCts.Token : CancellationToken.None;
        _requestQueue.Enqueue(new RequestEntry(commandQueue, token));

        _semaphore.Release();
    }

    /// <summary>
    /// Drains all non-cancelled entries from the request queue, processing their commands in order.
    /// </summary>
    private void ProcessQueue()
    {
        while (_requestQueue.TryDequeue(out var entry))
        {
            if (entry.CancellationToken.IsCancellationRequested)
            {
                continue;
            }

            foreach (var command in entry.Commands)
            {
                if (entry.CancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var frame = new RenderFrameCommand(command, entry.CancellationToken);
                _iteration(frame);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();

        var cts = Interlocked.Exchange(ref _skippableCts, null);
        cts?.Cancel();
        cts?.Dispose();

        // Drain the queue.
        while (_requestQueue.TryDequeue(out _))
        {
        }

        _semaphore.Dispose();
        _disposed = true;
    }
}
