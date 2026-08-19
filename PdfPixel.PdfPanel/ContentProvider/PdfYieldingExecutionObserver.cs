using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="PdfCancellationSourceExecutionObserver"/> that yields the thread.
/// </summary>
public sealed class PdfYieldingExecutionObserver : PdfCancellationSourceExecutionObserver
{
    /// <inheritdoc/>
    public override async ValueTask YieldAsync() => await Task.Yield();
}
