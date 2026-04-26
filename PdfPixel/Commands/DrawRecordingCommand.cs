using SkiaSharp;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Replays a recorded set of commands with a specified paint modifier.
/// When <c>disposeRecording</c> is true (default), the recorder and modifier are disposed with the command.
/// When false, the caller manages their lifetime (e.g. tiling patterns that replay the same recording many times).
/// </summary>
public sealed class DrawRecordingCommand : PdfCommand
{
    private readonly PdfCommandRecorder _recorder;
    private readonly IPdfCommandModifier _modifier;
    private readonly bool _disposeRecording;

    public DrawRecordingCommand(PdfCommandRecorder recorder, IPdfCommandModifier modifier, bool disposeRecording = true)
    {
        _recorder = recorder;
        _modifier = modifier;
        _disposeRecording = disposeRecording;
    }

    public DrawRecordingCommand(PdfCommandRecorder recorder, bool disposeRecording = true)
    {
        _recorder = recorder;
        _disposeRecording = disposeRecording;
    }

    /// <inheritdoc />
    public override bool IsScaleDependant => _recorder.Commands.Any(x => x.IsScaleDependant);

    /// <inheritdoc />
    public override async Task ExecuteAsync(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        // Append the recording-specific modifier so it composes on top of any outer modifiers.
        if (_modifier != null)
        {
            await _recorder.ReplayAsync(canvas, modifiers.Append(_modifier), executionContext);
        }
        else
        {
            await _recorder.ReplayAsync(canvas, modifiers, executionContext);
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
