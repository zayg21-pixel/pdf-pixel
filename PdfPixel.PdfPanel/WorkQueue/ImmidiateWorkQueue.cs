using System;

namespace PdfPixel.PdfPanel.WorkQueue;

public sealed class ImmidiateWorkQueue<T> : IWorkQueue<T> where T : IWorkItem
{
    public void Enqueue(T item)
    {
        try
        {
            item.Process();
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
