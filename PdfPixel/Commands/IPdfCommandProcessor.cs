using System;

namespace PdfPixel.Commands;

/// <summary>
/// Processes PDF drawing commands against a target.
/// </summary>
public interface IPdfCommandProcessor : IDisposable
{
    /// <summary>
    /// Submits a command for processing.
    /// </summary>
    /// <param name="command">The command to process.</param>
    void Process(IPdfCommand command);
}
