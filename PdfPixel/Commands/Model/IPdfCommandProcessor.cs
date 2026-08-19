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
}
