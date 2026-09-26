using System;

namespace PdfPixel.PdfPanel.Execution;

/// <summary>
/// <see cref="IPdfExecutionObserverFactory"/> that creates
/// <see cref="PdfYieldingExecutionObserver"/> instances.
/// </summary>
public sealed class PdfYieldingObserverFactory : IPdfExecutionObserverFactory
{
    private readonly TimeSpan _yieldInterval;

    /// <summary>
    /// Initializes the factory with the minimum time between two yields of the created observers.
    /// </summary>
#pragma warning disable RCS1231 // Make parameter ref read-only
    public PdfYieldingObserverFactory(TimeSpan yieldInterval) => _yieldInterval = yieldInterval;
#pragma warning restore RCS1231 // Make parameter ref read-only

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateParseObserver(int pageNumber) => new PdfYieldingExecutionObserver(_yieldInterval);

    /// <inheritdoc/>
    public IPdfCancellableExecutionObserver CreateContentObserver(int pageNumber) => new PdfYieldingExecutionObserver(_yieldInterval);
}
