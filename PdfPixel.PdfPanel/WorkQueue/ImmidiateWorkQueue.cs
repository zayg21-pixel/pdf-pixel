using Microsoft.Extensions.Logging;
using System;

namespace PdfPixel.PdfPanel.WorkQueue;

/// <summary>
/// Work queue that processes each item synchronously on the calling thread.
/// Intended for contexts where a dedicated worker thread is not available.
/// </summary>
public sealed class ImmidiateWorkQueue : IWorkQueue
{
    private readonly ILogger<ImmidiateWorkQueue> _logger;

    /// <summary>
    /// Initialises the queue with the logger used to report exceptions from processed items.
    /// </summary>
    public ImmidiateWorkQueue(ILogger<ImmidiateWorkQueue> logger) => _logger = logger;

    /// <inheritdoc />
    public void Enqueue(IWorkItem item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        try
        {
            item.Process();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while processing a work item.");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
