using System.Threading.Tasks;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Processes PDF drawing commands against a target.
/// </summary>
public interface IPdfCommandProcessor
{
    /// <summary>
    /// Submits a command for processing.
    /// </summary>
    /// <param name="command">The command to process.</param>
    void Process(IPdfCommand command);

    /// <summary>
    /// Submits a command for processing, yielding the thread through the execution observer.
    /// </summary>
    /// <param name="command">The command to process.</param>
    ValueTask ProcessAsync(IPdfCommand command);
}
