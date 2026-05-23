namespace PdfPixel.Commands;

/// <summary>
/// Interface that notifies that some chunk of work has been completed, new command parsed,
/// processed, image row processed, etc.
/// </summary>
public interface IPdfExecutionObserver
{
    /// <summary>
    /// Notifies that some work chunk has been done.
    /// </summary>
    void Notify();
}
