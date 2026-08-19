using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Records commands for later replay instead of executing them immediately.
/// </summary>
public sealed class PdfCommandRecorder : IPdfCommandProcessor
{
    private readonly List<IPdfCommand> _commands = [];

    /// <inheritdoc />
    public void Process(IPdfCommand command) => _commands.Add(command);

    /// <inheritdoc />
    public ValueTask ProcessAsync(IPdfCommand command)
    {
        Process(command);

        return default;
    }

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

    /// <summary>
    /// Replays all recorded commands by submitting each one to <paramref name="processor"/>.
    /// Marked-content visibility and error handling are the responsibility of <paramref name="processor"/>'s
    /// own <see cref="IPdfCommandProcessor.ProcessAsync"/>.
    /// </summary>
    /// <param name="processor">Processor each recorded command is submitted to.</param>
    public async ValueTask ReplayAsync(IPdfCommandProcessor processor)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        foreach (IPdfCommand command in _commands)
        {
            await processor.ProcessAsync(command).ConfigureAwait(false);
        }
    }
}
