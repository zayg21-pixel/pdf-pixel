using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Replays a recorded set of commands with a specified paint modifier.
/// When <c>disposeRecording</c> is true (default), the recorder and modifier are disposed with the command.
/// When false, the caller manages their lifetime (e.g. tiling patterns that replay the same recording many times).
/// </summary>
public sealed class DrawRecordingCommand : PdfCommand
{
    private readonly PdfCommandRecorder _recorder;
    private readonly IPdfCommandModifier? _modifier;
    private readonly bool _disposeRecording;

    /// <summary>
    /// Initializes the command with a recorder, an optional paint modifier, and ownership flag.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, IPdfCommandModifier? modifier, bool disposeRecording = true)
    {
        _recorder = recorder;
        _modifier = modifier;
        _disposeRecording = disposeRecording;
    }

    /// <summary>
    /// Initializes the command with a recorder and ownership flag; no paint modifier applied.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, bool disposeRecording = true)
    {
        _recorder = recorder;
        _disposeRecording = disposeRecording;
    }

    /// <inheritdoc />
    public override bool IsScaleDependent => _recorder.Commands.Any(x => x.IsScaleDependent);

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        // Append the recording-specific modifier so it composes on top of any outer modifiers.
        if (_modifier != null)
        {
            _recorder.Replay(canvas, modifiers.Append(_modifier), executionContext);
        }
        else
        {
            _recorder.Replay(canvas, modifiers, executionContext);
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_disposeRecording)
        {
            _recorder.Dispose();
        }

        _modifier?.Dispose();
    }
}
