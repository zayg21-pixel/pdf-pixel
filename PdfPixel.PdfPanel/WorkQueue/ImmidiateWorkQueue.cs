using System;

namespace PdfPixel.PdfPanel.WorkQueue;

public sealed class ImmidiateWorkQueue<T> : IWorkQueue<T> where T : IWorkItem
{
    public async void Enqueue(T item)
    {
        try
        {
            await item.ProcessAsync();
        }
        catch (ObjectDisposedException)
        {
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
