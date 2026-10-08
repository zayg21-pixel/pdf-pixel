using System.Collections.Concurrent;
using System.Diagnostics;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// Runs posted callbacks on the thread that calls <see cref="Run"/>, the way a UI thread runs them,
/// and logs every callback that holds the thread for <see cref="BlockedThreshold"/> or longer.
/// </summary>
internal sealed class PanelSynchronizationContext : SynchronizationContext, IDisposable
{
    private static readonly TimeSpan BlockedThreshold = TimeSpan.FromMilliseconds(50);

    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _callbacks = [];
    private readonly Stopwatch _clock;

    /// <summary>
    /// Initializes the context with the clock its log timestamps are read from.
    /// </summary>
    public PanelSynchronizationContext(Stopwatch clock) => _clock = clock;

    /// <inheritdoc />
    public override void Post(SendOrPostCallback d, object? state) => _callbacks.Add((d, state));

    /// <summary>
    /// Runs posted callbacks as they arrive until <paramref name="duration"/> has passed.
    /// </summary>
    public void Run(TimeSpan duration)
    {
        TimeSpan deadline = _clock.Elapsed + duration;

        while (true)
        {
            TimeSpan remaining = deadline - _clock.Elapsed;

            if (remaining <= TimeSpan.Zero
                || !_callbacks.TryTake(out (SendOrPostCallback Callback, object? State) item, remaining))
            {
                return;
            }

            TimeSpan start = _clock.Elapsed;
            item.Callback(item.State);
            TimeSpan blocked = _clock.Elapsed - start;

            if (blocked >= BlockedThreshold)
            {
                Console.WriteLine($"{start.TotalMilliseconds,10:F1} ms  blocked {blocked.TotalMilliseconds:F1} ms");
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() => _callbacks.Dispose();
}
