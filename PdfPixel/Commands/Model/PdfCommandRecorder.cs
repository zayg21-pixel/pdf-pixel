using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Records commands for later replay instead of executing them immediately.
/// </summary>
public sealed class PdfCommandRecorder : IPdfCommandProcessor
{
    private readonly List<IPdfCommand> _commands = [];

    /// <inheritdoc />
    public void Process(IPdfCommand command) => _commands.Add(command);

    /// <summary>
    /// Gets the list of recorded commands.
    /// </summary>
    public IReadOnlyList<IPdfCommand> Commands => _commands;

    /// <summary>
    /// Replays all recorded commands by submitting each one to <paramref name="processor"/>.
    /// Marked-content visibility and error handling are the responsibility of <paramref name="processor"/>'s
    /// own <see cref="IPdfCommandProcessor.Process"/>.
    /// </summary>
    /// <param name="processor">Processor each recorded command is submitted to.</param>
    public void Replay(IPdfCommandProcessor processor)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        foreach (IPdfCommand command in _commands)
        {
            processor.Process(command);
        }
    }
}
