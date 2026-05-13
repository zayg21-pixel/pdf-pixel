using System;

namespace PdfPixel.PdfPanel.WorkQueue;

public interface IWorkQueue : IDisposable
{
    void Enqueue(IWorkItem item);
}
