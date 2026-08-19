using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// <see cref="PdfCancellationSourceExecutionObserver"/> that never yields the thread.
/// </summary>
public sealed class PdfNonYieldingExecutionObserver : PdfCancellationSourceExecutionObserver
{
    /// <inheritdoc/>
    public override ValueTask YieldAsync() => default;
}
