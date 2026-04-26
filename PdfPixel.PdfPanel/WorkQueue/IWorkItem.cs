using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.WorkQueue;

public interface IWorkItem
{
    public bool IsSkippable { get; }

    public Task ProcessAsync();
}
