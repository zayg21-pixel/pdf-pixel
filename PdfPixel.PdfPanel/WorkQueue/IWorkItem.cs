using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.WorkQueue;

public interface IWorkItem
{
    public bool IsSkippable { get; }

    CancellationTokenSource CancellationTokenSource { get; }

    public Task ProcessAsync();
}
