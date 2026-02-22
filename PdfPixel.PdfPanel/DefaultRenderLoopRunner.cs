using PdfPixel.PdfPanel.Requests;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Default render loop runner that blocks on a semaphore waiting for work.
/// </summary>
public sealed class DefaultRenderLoopRunner : IRenderLoopRunner
{
    private bool _disposed;
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    private volatile bool _running;
    private Action<RenderFrameCommand> _iteration;

    // Latest pending request, exchanged atomically. Enqueue writes, Start loop reads.
    private volatile DrawingRequest _pendingRequest;

    // CTS owned by the render loop, cancelled when a new request is picked up.
    private volatile CancellationTokenSource _activeCts;

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

            // Take the pending request atomically
            var request = Interlocked.Exchange(ref _pendingRequest, null);
            if (request == null)
            {
                continue;
            }

            // Create fresh CTS for this frame (previous frame already completed)
            _activeCts?.Dispose();
            _activeCts = new CancellationTokenSource();

            var commands = PdfPanelRenderCommand.GenerateCommandsFromRequest(request);

            foreach (var command in commands)
            {
                if (_activeCts.IsCancellationRequested)
                {
                    break;
                }

                var frame = new RenderFrameCommand(command, _activeCts.Token);
                _iteration(frame);
            }
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

        // Cancel the currently rendering frame (running on render thread)
        _activeCts?.Cancel();

        Interlocked.Exchange(ref _pendingRequest, request);
        _semaphore.Release();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();

        _activeCts?.Cancel();
        _activeCts?.Dispose();
        _activeCts = null;

        _semaphore.Dispose();
        _disposed = true;
    }
}
