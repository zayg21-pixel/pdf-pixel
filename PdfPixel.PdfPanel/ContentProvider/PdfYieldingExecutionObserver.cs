using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="PdfCancellationSourceExecutionObserver"/> that yields the thread once the yield interval has passed since the last yield.
/// </summary>
public sealed class PdfYieldingExecutionObserver : PdfCancellationSourceExecutionObserver
{
    private readonly TimeSpan _yieldInterval;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    /// <summary>
    /// Initializes the observer with the minimum time between two yields.
    /// </summary>
#pragma warning disable RCS1231 // Make parameter ref read-only
    public PdfYieldingExecutionObserver(TimeSpan yieldInterval) => _yieldInterval = yieldInterval;
#pragma warning restore RCS1231 // Make parameter ref read-only

    /// <inheritdoc/>
    public override ValueTask YieldAsync()
    {
        if (_stopwatch.Elapsed < _yieldInterval)
        {
            return default;
        }

        return YieldThreadAsync();
    }

    private async ValueTask YieldThreadAsync()
    {
        await Task.Yield();
        _stopwatch.Restart();
    }
}
