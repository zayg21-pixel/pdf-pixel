using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PdfPixel.PdfPanel.WorkQueue;

public sealed class AsyncWorkQueue : IWorkQueue
{
    private readonly ConcurrentQueue<IWorkItem> _workItems = new ConcurrentQueue<IWorkItem>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    private readonly ILogger<AsyncWorkQueue> _logger;

    public AsyncWorkQueue(ILogger<AsyncWorkQueue> logger)
    {
        _logger = logger;
        ProcessingLoop();
    }

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

                if (!_workItems.TryDequeue(out IWorkItem workItem))
                    continue;

                if (workItem.IsSkippable)
                    continue;

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing a work item {ex}.", ex);
            }
        }

        _logger.LogInformation("AsyncWorkQueue processing loop stopped.");
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
