using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.WorkQueue;

/// <summary>
/// Represents a single unit of work that can be processed by an <see cref="IWorkQueue"/>.
/// </summary>
public interface IWorkItem
{
    /// <summary>
    /// When <see langword="true"/> the item may be dropped from the queue if newer work supersedes it.
    /// </summary>
    bool IsSkippable { get; }

    /// <summary>
    /// Executes the work item. Called on the worker thread.
    /// </summary>
    ValueTask ProcessAsync();
}
