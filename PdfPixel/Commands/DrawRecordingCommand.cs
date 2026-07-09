using System.Linq;

namespace PdfPixel.Commands;

/// <summary>
/// Replays a recorded set of commands with a specified paint modifier.
/// When <c>disposeRecording</c> is true (default), the recorder and modifier are disposed with the command.
/// When false, the caller manages their lifetime (e.g. tiling patterns that replay the same recording many times).
/// </summary>
public sealed class DrawRecordingCommand : PdfCommand
{
    private readonly bool _disposeRecording;

    /// <summary>
    /// Initializes the command with a recorder, an optional paint modifier, and ownership flag.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, UncoloredPaintModifier? modifier, bool disposeRecording = true)
    {
        Recorder = recorder;
        Modifier = modifier;
        _disposeRecording = disposeRecording;
    }

    /// <summary>
    /// Initializes the command with a recorder and ownership flag; no paint modifier applied.
    /// </summary>
    public DrawRecordingCommand(PdfCommandRecorder recorder, bool disposeRecording = true)
    {
        Recorder = recorder;
        _disposeRecording = disposeRecording;
    }

    /// <summary>
    /// Recorder used to draw command batch.
    /// </summary>
    public PdfCommandRecorder Recorder { get; }

    /// <summary>
    /// Modifier of a current command batch.
    /// </summary>
    public UncoloredPaintModifier? Modifier { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => Recorder.Commands.Aggregate(PdfCommandFeatures.None, (acc, cmd) => acc | cmd.Features);

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        int savesCountBefore = executionContext.Frames.SavesCount;
        UncoloredPaintModifier? previousModifier = executionContext.UncoloredModifier;

        if (Modifier != null)
        {
            executionContext.UncoloredModifier = Modifier;
        }

        Recorder.Replay(executionContext);

        BalanceFrames(executionContext, savesCountBefore);

        executionContext.UncoloredModifier = previousModifier;
    }

    /// <summary>
    /// Restores the save/restore stack to the depth it was at before the recording was replayed.
    /// Non-spec-compliant content streams can save more states than they restore, or restore more
    /// than they saved (popping into the caller's own saved state). Left unbalanced, either case
    /// would corrupt the canvas and frame state for every command that runs after this one.
    /// </summary>
    private static void BalanceFrames(PdfCommandExecutionContext executionContext, int savesCountBefore)
    {
        PdfCommandExecutionFrames frames = executionContext.Frames;

        while (frames.SavesCount > savesCountBefore)
        {
            executionContext.Canvas.Restore();
            frames.OnRestoreState();
        }

        while (frames.SavesCount < savesCountBefore)
        {
            executionContext.Canvas.Save();
            frames.OnSaveState();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_disposeRecording)
        {
            Recorder.Dispose();
        }

        Modifier?.Dispose();
    }
}
