using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PdfPixel.PdfPanel.WorkQueue;

public sealed class AsyncWorkQueue<T> : IWorkQueue<T> where T : IWorkItem
{
    private readonly ConcurrentQueue<T> _workItems = new ConcurrentQueue<T>();
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
    private readonly ILogger<AsyncWorkQueue<T>> _logger;

    public AsyncWorkQueue(ILogger<AsyncWorkQueue<T>> logger)
    {
        _logger = logger;
        ProcessingLoop();
    }

    public void Enqueue(T item)
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

                if (!_workItems.TryDequeue(out T workItem))
                {
                    continue;
                }

                if (workItem.CancellationTokenSource?.IsCancellationRequested == true)
                {
                    continue;
                }


                if (workItem.IsSkippable && !_workItems.IsEmpty)
                {
                    try
                    {
                        workItem.CancellationTokenSource?.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                    }

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
                    // silently ignore.
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing a work item.");
            }
        }

        _logger.LogInformation("AsyncWorkQueue processing loop stopped.");
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
