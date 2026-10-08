using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PdfPixel.PdfPanel.WorkQueue;

/// <summary>
/// Thread-safe work queue that processes <see cref="IWorkItem"/> instances one at a time, in order,
/// in an asynchronous loop that awaits a semaphore. Skippable items are discarded when the queue is drained past them.
/// </summary>
public sealed class SequentialWorkQueue : IWorkQueue
{
    private readonly ConcurrentQueue<IWorkItem> _workItems = [];
    private readonly SemaphoreSlim _semaphore = new(0);
    private readonly ILogger<SequentialWorkQueue> _logger;

    /// <summary>
    /// Initialises the queue and starts the processing loop.
    /// </summary>
    public SequentialWorkQueue(ILogger<SequentialWorkQueue> logger)
    {
        _logger = logger;
        ProcessingLoop();
    }

    /// <inheritdoc />
    public void Enqueue(IWorkItem item)
    {
        _workItems.Enqueue(item);
        _semaphore.Release();
    }

    private async void ProcessingLoop()
    {
        _logger.LogInformation("SequentialWorkQueue processing loop started.");

        while (true)
        {
            try
            {
                try
                {
                    await _semaphore.WaitAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (!_workItems.TryDequeue(out IWorkItem? workItem))
                {
                    continue;
                }

                if (workItem.IsSkippable)
                {
                    continue;
                }

                try
                {
                    await workItem.ProcessAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                }
                catch (OperationCanceledException)
                {
                }
            }
            catch (ObjectDisposedException)
            {
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing a work item.");
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("SequentialWorkQueue processing loop stopped.");
    }

    /// <inheritdoc />
    public void Dispose() => _semaphore.Dispose();
}
