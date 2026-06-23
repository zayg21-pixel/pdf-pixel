using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Records commands for later replay instead of executing them immediately.
/// </summary>
public sealed class PdfCommandRecorder : IPdfCommandProcessor
{
    private readonly List<IPdfCommand> _commands = [];

    /// <inheritdoc/>
    ~PdfCommandRecorder() => Dispose(disposing: false);

    /// <inheritdoc />
    public void Process(IPdfCommand command) => _commands.Add(command);

    /// <summary>
    /// Gets the list of recorded commands.
    /// </summary>
    public IReadOnlyList<IPdfCommand> Commands => _commands;

    /// <summary>
    /// Replays all recorded commands using the canvas and rendering parameters from <paramref name="executionContext"/>.
    /// </summary>
    /// <param name="modifiers">The modifiers to apply during replay, applied in order.</param>
    /// <param name="executionContext">Execution-time context containing the canvas, rendering parameters, and cancellation.</param>
    public void Replay(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        foreach (IPdfCommand command in _commands)
        {
            if (!executionContext.MarkedContent.ShouldExecute(command))
            {
                continue;
            }

            command.Execute(modifiers, executionContext);
            executionContext.CurrentCommand = command;
            executionContext.ExecutionObserver?.Notify();
        }
    }

    private void Dispose(bool disposing)
    {
        foreach (IPdfCommand command in _commands)
        {
            command.Dispose();
        }

        _commands.Clear();
    }

    /// <summary>
    /// Disposes all recorded commands and clears the list.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
