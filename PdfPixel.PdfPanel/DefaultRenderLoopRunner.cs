using PdfPixel.PdfPanel.Requests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

    // Latest pending request with pre-generated commands and fresh CTS, exchanged atomically.
    // Enqueue writes and cancels the previous; the render loop takes ownership by swapping to null.
    private volatile RequestAndToken _pendingRequest;

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

            var request = _pendingRequest;
            if (request == null)
            {
                continue;
            }

            foreach (var command in request.Commands)
            {
                if (request.CancellationTokenSource.IsCancellationRequested)
                {
                    break;
                }

                var frame = new RenderFrameCommand(command, request.CancellationTokenSource.Token);
                _iteration(frame);
            }

            // Only clear and dispose if no newer request has replaced this one.
            // If Enqueue already swapped in a new request, it owns and has disposed the old CTS.
            var replaced = Interlocked.CompareExchange(ref _pendingRequest, null, request);
            if (replaced == request)
            {
                request.CancellationTokenSource.Dispose();
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

        var commands = PdfPanelRenderCommand.GenerateCommandsFromRequest(request);
        var newRequest = new RequestAndToken(commands, new CancellationTokenSource());

        var oldRequest = Interlocked.Exchange(ref _pendingRequest, newRequest);
        oldRequest?.CancellationTokenSource.Cancel();
        oldRequest?.CancellationTokenSource.Dispose();

        _semaphore.Release();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();

        var pendingRequest = Interlocked.Exchange(ref _pendingRequest, null);
        pendingRequest?.CancellationTokenSource.Cancel();
        pendingRequest?.CancellationTokenSource.Dispose();

        _semaphore.Dispose();
        _disposed = true;
    }

    private sealed class RequestAndToken
    {
        public RequestAndToken(List<PdfPanelRenderCommand> commands, CancellationTokenSource cancellationTokenSource)
        {
            Commands = commands;
            CancellationTokenSource = cancellationTokenSource;
        }

        /// <summary>
        /// The pre-generated list of render commands to execute for this request.
        /// </summary>
        public List<PdfPanelRenderCommand> Commands { get; }

        /// <summary>
        /// The cancellation token source for this request, cancelled when a newer request replaces it.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; }
    }
}
