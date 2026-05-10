using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.WorkQueue;

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
            catch (ObjectDisposedException)
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
