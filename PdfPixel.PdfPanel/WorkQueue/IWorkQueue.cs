using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.WorkQueue
{
    // TODO: document
    public interface IWorkItem
    {
        public bool IsSkippable { get; }

        CancellationTokenSource CancellationTokenSource { get; }

        public void Process();
    }

    public interface IWorkQueue<T> : IDisposable where T : IWorkItem
    {
        void Enqueue(T item);
    }

    public sealed class ImmidiateWorkQueue<T> : IWorkQueue<T> where T : IWorkItem
    {
        public void Enqueue(T item)
        {
            try
            {
                item.Process();
            }
            catch (OperationCanceledException)
            {
                // silently ignore.
            }
            catch
            {
                // TODO: log
            }
        }

        void IDisposable.Dispose()
        {
        }
    }

    public sealed class AsyncMultiProcessWorkQueue<T> : IWorkQueue<T> where T : IWorkItem
    {
        private readonly ILogger<AsyncMultiProcessWorkQueue<T>> _logger;

        public AsyncMultiProcessWorkQueue(ILogger<AsyncMultiProcessWorkQueue<T>> logger)
        {
            _logger = logger;
        }

        public void Enqueue(T item)
        {
            Task.Run(() =>
            {
                try
                {
                    item.Process();
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing a work item.");
                }
            });
        }

        void IDisposable.Dispose()
        {
        }
    }

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
                        workItem.Process();
                    }
                    catch (OperationCanceledException)
                    {
                        // silently ignore.
                    }
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

    public sealed class SeparateThreadWorkQueue<T> : IWorkQueue<T> where T : IWorkItem
    {
        private readonly Thread _workerThread;
        private readonly ConcurrentQueue<T> _workItems = new ConcurrentQueue<T>();
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private readonly ILogger<SeparateThreadWorkQueue<T>> _logger;

        public SeparateThreadWorkQueue(ILogger<SeparateThreadWorkQueue<T>> logger)
        {
            _logger = logger;
            _workerThread = new Thread(ProcessingLoop)
            {
                IsBackground = true
            };
            _workerThread.Start();
        }
        public void Enqueue(T item)
        {
            _workItems.Enqueue(item);
            _semaphore.Release();
        }

        private void ProcessingLoop()
        {
            _logger.LogInformation("Work queue processing loop started.");

            while (true)
            {
                try
                {
                    try
                    {
                        _semaphore.Wait();
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
                        workItem.Process();
                    }
                    catch (OperationCanceledException)
                    {
                        // silently ignore.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing a work item.");
                }
            }

            _logger.LogInformation("Work queue processing loop stopped.");
        }

        public void Dispose()
        {
            _semaphore.Dispose();
        }
    }
}
