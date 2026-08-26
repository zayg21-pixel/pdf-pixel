using System;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Animation;

/// <summary>
/// Global animation timer. Runs indefinitely on a background thread and fires <see cref="Tick"/>
/// from the background thread at the requested frame rate. Zero UI-thread pressure when no subscribers are attached.
/// </summary>
internal sealed class PdfAnimationClock : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Initializes the clock and starts the background loop.
    /// </summary>
    /// <param name="fps">Target frames per second.</param>
    public PdfAnimationClock(int fps)
    {
        Fps = (fps > 0) ? fps : throw new ArgumentOutOfRangeException(nameof(fps));
        _ = RunAsync(_cts.Token);
    }

    /// <summary>
    /// Target frames per second this clock was configured with.
    /// Use together with <see cref="AnimationTickEventArgs.Tick"/> to compute animation state.
    /// </summary>
    public int Fps { get; }

    /// <summary>
    /// Fires from the background thread on every tick.
    /// Use <see cref="AnimationTickEventArgs.Tick"/> with <see cref="Fps"/> to derive any animation value.
    /// Subscribers are responsible for marshaling to the UI thread if needed.
    /// Subscribe only while animation is needed; unsubscribe when idle.
    /// </summary>
    public event EventHandler<AnimationTickEventArgs>? Tick;

    private async Task RunAsync(CancellationToken token)
    {
        int delayMs = 1000 / Fps;
        long tick = 0;

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

            tick++;
            Tick?.Invoke(this, new AnimationTickEventArgs(tick));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}

