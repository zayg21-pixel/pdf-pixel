using System;

namespace PdfPixel.PdfPanel.WorkQueue;

/// <summary>
/// Work queue that processes each item synchronously on the calling thread.
/// Intended for contexts where a dedicated worker thread is not available.
/// </summary>
public sealed class ImmidiateWorkQueue : IWorkQueue
{
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
        catch
        {
            // TODO: log
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
