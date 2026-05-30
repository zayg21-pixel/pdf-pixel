using System;

namespace PdfPixel.PdfPanel.WorkQueue;

/// <summary>
/// Queues <see cref="IWorkItem"/> instances for background processing.
/// </summary>
public interface IWorkQueue : IDisposable
{
    /// <summary>
    /// Adds <paramref name="item"/> to the processing queue.
    /// </summary>
    void Enqueue(IWorkItem item);
}
