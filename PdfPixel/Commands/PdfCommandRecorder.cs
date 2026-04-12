using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Records commands for later replay instead of executing them immediately.
/// Tracks the current transformation matrix as <see cref="ConcatMatrixCommand"/> instances are recorded.
/// </summary>
public sealed class PdfCommandRecorder : IPdfCommandProcessor
{
    private readonly List<IPdfCommand> _commands = new();
    private SKMatrix _totalMatrix = SKMatrix.Identity;

    ~PdfCommandRecorder()
    {
        Dispose(disposing: false);
    }

    /// <inheritdoc />
    public void Process(IPdfCommand command)
    {
        if (command is ConcatMatrixCommand concatCommand)
        {
            _totalMatrix = _totalMatrix.PostConcat(concatCommand.Matrix);
        }

        _commands.Add(command);
    }

    /// <summary>
    /// Gets the list of recorded commands.
    /// </summary>
    public IReadOnlyList<IPdfCommand> Commands => _commands;

    /// <summary>
    /// Gets the current total transformation matrix accumulated from recorded
    /// <see cref="ConcatMatrixCommand"/> instances.
    /// </summary>
    public SKMatrix TotalMatrix => _totalMatrix;

    /// <summary>
    /// Replays all recorded commands against the specified canvas, modifiers, and execution context.
    /// </summary>
    /// <param name="canvas">The canvas to replay commands onto.</param>
    /// <param name="modifiers">The modifiers to apply during replay, applied in order.</param>
    /// <param name="executionContext">Execution-time context containing rendering parameters and cancellation.</param>
    public void Replay(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        foreach (var command in _commands)
        {
            command.Execute(canvas, modifiers, executionContext);
            executionContext.CancellationToken.ThrowIfCancellationRequested();
        }
    }

    private void Dispose(bool disposing)
    {
        foreach (var command in _commands)
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
