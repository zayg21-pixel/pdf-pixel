using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Animation;

/// <summary>
/// Global animation timer. Runs indefinitely on a background thread and fires <see cref="Tick"/>
/// on the UI thread at the requested frame rate. Zero UI-thread pressure when no subscribers are attached.
/// </summary>
public sealed class PdfAnimationClock : IDisposable
{
    private readonly SynchronizationContext _uiContext;
    private readonly CancellationTokenSource _cts = new();
    private long _tick;

    /// <summary>
    /// The current tick count, safe to read from any thread.
    /// </summary>
    public long CurrentTick => Interlocked.Read(ref _tick);

    /// <summary>
    /// Initializes the clock and starts the background loop.
    /// Must be constructed on the UI thread.
    /// </summary>
    /// <param name="uiContext">UI synchronization context to marshal tick events onto.</param>
    /// <param name="fps">Target frames per second. Default is 60.</param>
    public PdfAnimationClock(SynchronizationContext uiContext, int fps = 60)
    {
        _uiContext = uiContext ?? throw new ArgumentNullException(nameof(uiContext));
        Fps = (fps > 0) ? fps : throw new ArgumentOutOfRangeException(nameof(fps));
        _ = RunAsync(_cts.Token);
    }

    /// <summary>
    /// Target frames per second this clock was configured with.
    /// Use together with <see cref="AnimationTickEventArgs.Tick"/> to compute animation state.
    /// </summary>
    public int Fps { get; }

    /// <summary>
    /// Fires on the UI thread on every tick.
    /// Use <see cref="AnimationTickEventArgs.Tick"/> with <see cref="Fps"/> to derive any animation value.
    /// Subscribe only while animation is needed; unsubscribe when idle.
    /// </summary>
    public event EventHandler<AnimationTickEventArgs>? Tick;

    private async Task RunAsync(CancellationToken token)
    {
        int delayMs = 1000 / Fps;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delayMs, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            long tick = Interlocked.Increment(ref _tick);

            if (Tick != null)
            {
                _uiContext.Post(_ => Tick?.Invoke(this, new AnimationTickEventArgs(tick)), null);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

