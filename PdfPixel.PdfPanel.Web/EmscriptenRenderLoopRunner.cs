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

    /// <summary>
    /// Cross-thread queue: produced by <see cref="Enqueue"/> on thread 1,
    /// consumed by <see cref="OnFrame"/> on thread 2.
    /// </summary>
    private readonly ConcurrentQueue<RequestEntry> _requestQueue = new();

    /// <summary>
    /// Shared <see cref="CancellationTokenSource"/> for all pending skippable requests.
    /// Replaced each time a new request is enqueued, cancelling all previously queued skippable work.
    /// </summary>
    private CancellationTokenSource _skippableCts = new();

    /// <summary>
    /// Entry currently being drained by <see cref="OnFrame"/>. Accessed only from thread 2.
    /// </summary>
    private RequestEntry _currentEntry;

    // Instances keyed by thread ID - each render thread has its own runner
    private static readonly ConcurrentDictionary<int, EmscriptenRenderLoopRunner> Instances = new();

    /// <summary>
    /// Creates a new <see cref="EmscriptenRenderLoopRunner"/>.
    /// </summary>
    public EmscriptenRenderLoopRunner()
    {
    }

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

        // Cancel all previously queued skippable requests.
        var newCts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _skippableCts, newCts);
        oldCts.Cancel();
        oldCts.Dispose();

        // Non-skippable requests receive CancellationToken.None so they are never cancelled.
        var token = request.IsSkippable ? newCts.Token : CancellationToken.None;
        _requestQueue.Enqueue(new RequestEntry(commandQueue, token));
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

        // Advance to the next non-cancelled entry when the current one is exhausted.
        while (instance._currentEntry == null || instance._currentEntry.Commands.Count == 0)
        {
            instance._currentEntry = null;

            if (!instance._requestQueue.TryDequeue(out var next))
            {
                return;
            }

            if (!next.CancellationToken.IsCancellationRequested)
            {
                instance._currentEntry = next;
                break;
            }
        }

        var entry = instance._currentEntry;

        if (entry == null)
        {
            return;
        }

        // The entry may have been cancelled while earlier commands were being processed.
        if (entry.CancellationToken.IsCancellationRequested)
        {
            instance._currentEntry = null;
            return;
        }

        if (entry.Commands.TryDequeue(out var renderCommand))
        {
            var frame = new RenderFrameCommand(renderCommand, entry.CancellationToken);
            instance._iteration?.Invoke(frame);
        }

        if (entry.Commands.Count == 0)
        {
            instance._currentEntry = null;
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

        _currentEntry = null;
        _disposed = true;
    }

    }
