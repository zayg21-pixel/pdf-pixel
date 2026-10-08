using System;

namespace PdfPixel.PdfPanel.WorkQueue;

/// <summary>
/// Queues <see cref="IWorkItem"/> instances for processing.
/// Implementations decide when, and on which thread, each item runs.
/// </summary>
public interface IWorkQueue : IDisposable
{
    /// <summary>
    /// Adds <paramref name="item"/> to the processing queue.
    /// </summary>
    void Enqueue(IWorkItem item);
}
