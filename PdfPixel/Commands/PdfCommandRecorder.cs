using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

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
    /// Returns the area the recorded commands paint, in the space they are recorded in: the union of the area of every
    /// paint command and nested recording, each mapped through the matrix in effect where it is recorded.
    /// <see cref="PdfRectangle.Empty"/> when nothing is painted, <see langword="null"/> when a command is not bounded.
    /// </summary>
    public PdfRectangle? GetBounds()
    {
        Stack<PdfMatrix> savedMatrices = [];
        PdfMatrix matrix = PdfMatrix.Identity;
        PdfRectangle? bounds = null;

        foreach (IPdfCommand command in _commands)
        {
            PdfRectangle? commandBounds;

            switch (command.Kind)
            {
                case PdfCommandKind.SaveState:
                {
                    savedMatrices.Push(matrix);
                    continue;
                }
                case PdfCommandKind.RestoreState:
                {
                    if (savedMatrices.Count > 0)
                    {
                        matrix = savedMatrices.Pop();
                    }

                    continue;
                }
                case PdfCommandKind.ConcatMatrix:
                {
                    matrix = matrix.PreConcat(((ConcatMatrixCommand)command).Matrix);
                    continue;
                }
                case PdfCommandKind.DrawRecording:
                {
                    var recordingCommand = (DrawRecordingCommand)command;
                    PdfRectangle? recordingBounds = recordingCommand.Recorder.GetBounds();
                    commandBounds = null;

                    if (recordingBounds != null)
                    {
                        commandBounds = recordingCommand.Matrix.MapRect(recordingBounds.Value);
                    }

                    break;
                }
                case PdfCommandKind.DrawAnnotation:
                {
                    commandBounds = null;
                    break;
                }
                default:
                {
                    var paintCommand = command as IPaintCommand;

                    if (paintCommand == null || paintCommand.Paint?.Style == PdfPaintStyle.Clip)
                    {
                        continue;
                    }

                    commandBounds = paintCommand.Bounds;
                    break;
                }
            }

            if (commandBounds == null)
            {
                return null;
            }

            PdfRectangle mappedBounds = matrix.MapRect(commandBounds.Value);

            if (bounds == null)
            {
                bounds = mappedBounds;
            }
            else
            {
                bounds = PdfRectangle.Union(bounds.Value, mappedBounds);
            }
        }

        if (bounds == null)
        {
            return PdfRectangle.Empty;
        }

        return bounds;
    }

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
