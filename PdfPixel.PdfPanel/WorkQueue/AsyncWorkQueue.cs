using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PdfPixel.PdfPanel.WorkQueue;

/// <summary>
/// Thread-safe work queue that processes <see cref="IWorkItem"/> instances on a background thread.
/// Skippable items are discarded when the queue is drained past them.
/// </summary>
public sealed class AsyncWorkQueue : IWorkQueue
{
    private readonly ConcurrentQueue<IWorkItem> _workItems = [];
    private readonly SemaphoreSlim _semaphore = new(0);
    private readonly ILogger<AsyncWorkQueue> _logger;

    /// <summary>
    /// Initialises the queue and starts the background processing loop.
    /// </summary>
    public AsyncWorkQueue(ILogger<AsyncWorkQueue> logger)
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
        _logger.LogInformation("AsyncWorkQueue processing loop started.");

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
                    workItem.Process();
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
                _logger.LogError(ex, "An error occurred while processing a work item {Ex}.", ex);
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation("AsyncWorkQueue processing loop stopped.");
    }

    /// <inheritdoc />
    public void Dispose() => _semaphore.Dispose();
}
