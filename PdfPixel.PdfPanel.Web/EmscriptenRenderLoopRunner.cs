using PdfPixel.PdfPanel.Requests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Browser render loop runner that uses <c>emscripten_set_main_loop</c>.
/// Yields to the browser event loop between frames, allowing WebGL compositing.
/// Each instance manages its own latest request and is associated with a specific thread.
/// </summary>
[SupportedOSPlatform("browser")]
public sealed class EmscriptenRenderLoopRunner : IRenderLoopRunner
{
    private static readonly object SyncRoot = new object();
    private bool _disposed;
    private volatile bool _isRunning;
    private Action<RenderFrameCommand> _iteration;
    private int _threadId;
    private volatile RequestAndToken _pendingRequest;

    // Instances keyed by thread ID - each render thread has its own runner
    private static readonly ConcurrentDictionary<int, EmscriptenRenderLoopRunner> Instances = new();

    /// <inheritdoc />
    public unsafe void Start(Action<RenderFrameCommand> iteration)
    {
        if (_disposed)
        {
            return;
        }

        _iteration = iteration ?? throw new ArgumentNullException(nameof(iteration));
        _threadId = Environment.CurrentManagedThreadId;
        _isRunning = true;

        lock (SyncRoot)
        {
            bool wasEmpty = Instances.IsEmpty;
            Instances[_threadId] = this;

            // Start the main loop only when first instance registers
            if (wasEmpty)
            {
                Emscripten.StartMainLoop(&OnFrame, fps: 0, simulateInfiniteLoop: 1);
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

        _isRunning = false;

        lock (SyncRoot)
        {
            Instances.TryRemove(_threadId, out _);

            // Stop the main loop only when last instance unregisters
            if (Instances.IsEmpty)
            {
                Emscripten.StopMainLoop();
            }
        }
    }

    /// <inheritdoc />
    public void Enqueue(DrawingRequest request)
    {
        if (_disposed || !_isRunning)
        {
            return;
        }

        var commands = PdfPanelRenderCommand.GenerateCommandsFromRequest(request);
        var commandQueue = new Queue<PdfPanelRenderCommand>(commands);
        var newRequest = new RequestAndToken(commandQueue, new CancellationTokenSource());

        var oldRequest = Interlocked.Exchange(ref _pendingRequest, newRequest);
        oldRequest?.CancellationTokenSource.Cancel();
        oldRequest?.CancellationTokenSource.Dispose();
    }

    [UnmanagedCallersOnly]
    private static void OnFrame()
    {
        var threadId = Environment.CurrentManagedThreadId;

        if (!Instances.TryGetValue(threadId, out var instance))
        {
            return;
        }

        if (instance._disposed || !instance._isRunning)
        {
            return;
        }

        var request = instance._pendingRequest;

        if (request == null || request.CancellationTokenSource.IsCancellationRequested)
        {
            return;
        }

        if (request.Commands.TryDequeue(out var renderCommand))
        {
            var frame = new RenderFrameCommand(renderCommand, request.CancellationTokenSource.Token);
            instance._iteration?.Invoke(frame);
        }

        if (request.Commands.Count == 0)
        {
            // Only clear and dispose if no newer request has replaced this one.
            // If Enqueue already swapped in a new request, it owns and has disposed the old CTS.
            var replaced = Interlocked.CompareExchange(ref instance._pendingRequest, null, request);
            if (replaced == request)
            {
                request.CancellationTokenSource.Dispose();
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

        var pendingRequest = Interlocked.Exchange(ref _pendingRequest, null);
        pendingRequest?.CancellationTokenSource.Cancel();
        pendingRequest?.CancellationTokenSource.Dispose();

        _disposed = true;
    }

    private class RequestAndToken
    {
        public RequestAndToken(Queue<PdfPanelRenderCommand> commands, CancellationTokenSource cancellationTokenSource)
        {
            Commands = commands;
            CancellationTokenSource = cancellationTokenSource;
        }

        public Queue<PdfPanelRenderCommand> Commands { get; }

        public CancellationTokenSource CancellationTokenSource { get; }
    }
}
