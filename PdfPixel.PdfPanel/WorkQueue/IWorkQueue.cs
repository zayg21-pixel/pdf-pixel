using System;

namespace PdfPixel.PdfPanel.WorkQueue;

public interface IWorkQueue<T> : IDisposable where T : IWorkItem
{
    void Enqueue(T item);
}
