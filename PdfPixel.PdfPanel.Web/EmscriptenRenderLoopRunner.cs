using Microsoft.Extensions.Logging;
using PdfPixel.PdfPanel.Requests;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
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
    /// <summary>
    /// Number of commands processed between statistics dumps.
    /// </summary>
    private const int StatsDumpInterval = 200;

    private static readonly object SyncRoot = new object();
    private bool _disposed;
    private volatile bool _isRunning;
    private Action<RenderFrameCommand> _iteration;
    private int _threadId;
    private volatile RequestAndToken _pendingRequest;

    private readonly ILogger<EmscriptenRenderLoopRunner> _logger;

    // Per-instance statistics: accumulated count and total processing time per command type.
    // Accessed only from the owning render thread, so no locking is required.
    private int _commandsProcessedSinceLastDump;
    private readonly Dictionary<PdfPanelRenderCommandType, (int Count, double TotalMs)> _statsPerType = new();

    // Instances keyed by thread ID - each render thread has its own runner
    private static readonly ConcurrentDictionary<int, EmscriptenRenderLoopRunner> Instances = new();

    /// <summary>
    /// Creates a new <see cref="EmscriptenRenderLoopRunner"/>.
    /// </summary>
    /// <param name="logger">The logger used to emit periodic render statistics.</param>
    public EmscriptenRenderLoopRunner(ILogger<EmscriptenRenderLoopRunner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
            var startTimestamp = Stopwatch.GetTimestamp();
            instance._iteration?.Invoke(frame);
            var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
            instance.RecordStats(renderCommand.Type, elapsedMs);
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

    /// <summary>
    /// Records processing time for a single command and triggers a statistics dump every
    /// <see cref="StatsDumpInterval"/> commands.
    /// </summary>
    /// <param name="commandType">The type of command that was processed.</param>
    /// <param name="elapsedMs">The time taken to process the command, in milliseconds.</param>
    private void RecordStats(PdfPanelRenderCommandType commandType, double elapsedMs)
    {
        if (_statsPerType.TryGetValue(commandType, out var existing))
        {
            _statsPerType[commandType] = (existing.Count + 1, existing.TotalMs + elapsedMs);
        }
        else
        {
            _statsPerType[commandType] = (1, elapsedMs);
        }

        _commandsProcessedSinceLastDump++;

        if (_commandsProcessedSinceLastDump >= StatsDumpInterval)
        {
            DumpStats();
        }
    }

    /// <summary>
    /// Logs accumulated per-type statistics as a single message and resets the accumulators.
    /// </summary>
    private void DumpStats()
    {
        var builder = new StringBuilder();
        builder.Append($"Render loop statistics ({_commandsProcessedSinceLastDump} commands):");

        foreach (var (type, stats) in _statsPerType)
        {
            var averageMs = stats.Count > 0 ? stats.TotalMs / stats.Count : 0.0;
            builder.Append($"\n  {type}: count={stats.Count}, avg={averageMs:F2}ms");
        }

        _logger.LogInformation(builder.ToString());

        _statsPerType.Clear();
        _commandsProcessedSinceLastDump = 0;
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
