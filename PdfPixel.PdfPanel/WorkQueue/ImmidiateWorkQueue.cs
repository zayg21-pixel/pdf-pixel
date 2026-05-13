using System;

namespace PdfPixel.PdfPanel.WorkQueue;

public sealed class ImmidiateWorkQueue : IWorkQueue
{
    public void Enqueue(IWorkItem item)
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
