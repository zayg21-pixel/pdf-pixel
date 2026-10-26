using System.Threading.Tasks;

namespace PdfPixel.PdfPanel;

/// <summary>
/// <see cref="PdfCancellationSourceExecutionObserver"/> that never yields the thread.
/// </summary>
public sealed class PdfNonYieldingExecutionObserver : PdfCancellationSourceExecutionObserver
{
    /// <inheritdoc/>
    public override ValueTask YieldAsync() => default;
}
