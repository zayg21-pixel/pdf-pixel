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
    private bool _disposed;
    private static readonly object SyncRoot = new object();
    private Action<RenderFrameCommand> _iteration;
    private int _threadId;

    // Latest pending request, exchanged atomically. Enqueue writes, OnFrame reads.
    private volatile Queue<PdfPanelRenderCommand> _pendingRequestCommands;

    // CTS owned by the render loop, cancelled when a new request is picked up.
    private volatile CancellationTokenSource _activeCts;

    // Instances keyed by thread ID - each render thread has its own runner
    private static readonly ConcurrentDictionary<int, EmscriptenRenderLoopRunner> Instances = new();

    /// <inheritdoc />
    public unsafe void Start(Action<RenderFrameCommand> iteration)
    {
        _iteration = iteration ?? throw new ArgumentNullException(nameof(iteration));
        _threadId = Environment.CurrentManagedThreadId;

        lock (SyncRoot)
        {
            bool wasEmpty = Instances.IsEmpty;
            Instances[_threadId] = this;

            // Start the main loop only when first instance registers
            if (wasEmpty)
            {
                Emscripten.StartMainLoop(&OnFrame, fps: 60, simulateInfiniteLoop: 1);
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
        if (_disposed)
        {
            return;
        }

        // Cancel the currently rendering frame (running on render thread)
        _activeCts?.Cancel();

        var commands = PdfPanelRenderCommand.GenerateCommandsFromRequest(request);

        var commandQueue = new Queue<PdfPanelRenderCommand>(commands);
        Interlocked.Exchange(ref _pendingRequestCommands, commandQueue);
    }

    [UnmanagedCallersOnly]
    private static void OnFrame()
    {
        var threadId = Environment.CurrentManagedThreadId;

        if (!Instances.TryGetValue(threadId, out var instance))
        {
            return;
        }

        if (instance._disposed)
        {
            return;
        }

        var commands = instance._pendingRequestCommands;

        if (commands == null || commands.Count == 0)
        {
            return;
        }

        var activeCts  = instance._activeCts;
        instance._activeCts = new CancellationTokenSource();

        if (commands.TryDequeue(out var renderCommand))
        {
            var frame = new RenderFrameCommand(renderCommand, instance._activeCts.Token);
            instance._iteration?.Invoke(frame);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _activeCts?.Cancel();
        _activeCts?.Dispose();
        _activeCts = null;

        Stop();

        _disposed = true;
    }
}
