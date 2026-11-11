using Microsoft.Extensions.Logging;
using PdfPixel.Annotations;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Tagging;
using System;
using System.Threading.Tasks;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    /// <inheritdoc />
    public void Process(IPdfCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        _executionContext.ExecutionObserver.Notify();

        ExecuteCommand(command);
    }

    /// <inheritdoc />
    public async ValueTask ProcessAsync(IPdfCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        _executionContext.ExecutionObserver.Notify();

        await _executionContext.ExecutionObserver.YieldAsync().ConfigureAwait(false);

        await ExecuteCommandAsync(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a command, replaying a recording command by command with a yield between them; the content lock is
    /// never held across a yield.
    /// </summary>
    private async ValueTask ExecuteCommandAsync(IPdfCommand command)
    {
        if (command.Kind != PdfCommandKind.DrawRecording)
        {
            ExecuteCommand(command);

            return;
        }

        if (!_executionContext.ContentScopes.ShouldExecute(command))
        {
            return;
        }

        var recordingCommand = (DrawRecordingCommand)command;
        int savesAfterOwnSave = BeginDrawRecording(recordingCommand.TintPaint, recordingCommand.Matrix);

        try
        {
            foreach (IPdfCommand recordedCommand in recordingCommand.Recorder.Commands)
            {
                await ExecuteCommandAsync(recordedCommand).ConfigureAwait(false);

                _executionContext.ExecutionObserver.Notify();

                await _executionContext.ExecutionObserver.YieldAsync().ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            DiscardTextObject(savesAfterOwnSave);
            throw;
        }

        EndDrawRecording(savesAfterOwnSave);
    }

    private void ExecuteCommand(IPdfCommand command)
    {
        if (!_executionContext.ContentScopes.ShouldExecute(command))
        {
            return;
        }

        try
        {
            ProcessInternal(command);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception exception)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogWarning(exception, "Command {CommandType} failed during execution.", command.GetType().Name);
        }
    }

    private void ProcessInternal(IPdfCommand command)
    {
        switch (command.Kind)
        {
            case PdfCommandKind.BeginScope:
            {
                PdfScope scope = ((BeginScopeCommand)command).Scope;
                _executionContext.ContentScopes.Push(scope);

                if (!_state.IsLocal && _executionContext.ContentScopes.IsContentVisible)
                {
                    _executionContext.ContentTree.OpenNode(scope);
                }

                break;
            }
            case PdfCommandKind.BeginTextObject:
            {
                BeginTextObject(((BeginTextObjectCommand)command).Knockout);
                break;
            }
            case PdfCommandKind.ClearSoftMask:
            {
                _state.SetSoftMask(null);
                break;
            }
            case PdfCommandKind.ConcatMatrix:
            {
                Concat(((ConcatMatrixCommand)command).Matrix);
                break;
            }
            case PdfCommandKind.Content:
            {
                if (!_state.IsLocal)
                {
                    _executionContext.ContentTree.AddContent((IPdfContentCommand)command, _state.DeviceMatrix);
                }

                break;
            }
            case PdfCommandKind.DrawGroup:
            {
                var groupCommand = (DrawGroupCommand)command;
                DrawGroup(groupCommand.Source, groupCommand.Paint);
                break;
            }
            case PdfCommandKind.DrawImage:
            {
                var imageCommand = (DrawImageCommand)command;

                lock (_executionContext.ContentLocker)
                {
                    DrawImage(imageCommand.Image, imageCommand.Paint, imageCommand.Source);
                }

                break;
            }
            case PdfCommandKind.DrawPath:
            {
                var pathCommand = (DrawPathCommand)command;
                DrawPath(pathCommand.Path, pathCommand.Paint, pathCommand.Bounds, pathCommand.Source);
                break;
            }
            case PdfCommandKind.DrawShading:
            {
                var shadingCommand = (DrawShadingCommand)command;
                DrawShading(shadingCommand.Content, shadingCommand.Paint);
                break;
            }
            case PdfCommandKind.DrawRecording:
            {
                ExecuteDrawRecording((DrawRecordingCommand)command);
                break;
            }
            case PdfCommandKind.DrawAnnotation:
            {
                ExecuteDrawAnnotation((DrawAnnotationCommand)command);
                break;
            }
            case PdfCommandKind.DrawShapedText:
            {
                var textCommand = (DrawShapedTextCommand)command;
                DrawText(textCommand.ShapingResult, textCommand.Matrix, textCommand.Paint, textCommand.Bounds, textCommand.Source);
                break;
            }
            case PdfCommandKind.EndScope:
            {
                PdfScope? scope = _executionContext.ContentScopes.Pop();

                if (scope != null)
                {
                    _executionContext.ContentTree.CloseNode(scope);
                }

                break;
            }
            case PdfCommandKind.EndTextObject:
            {
                CloseTextObject();
                break;
            }
            case PdfCommandKind.RestoreState:
            {
                Restore();
                break;
            }
            case PdfCommandKind.SaveState:
            {
                Save();
                break;
            }
            case PdfCommandKind.SetSoftMask:
            {
                SetSoftMask(((SetSoftMaskCommand)command).Mask);
                break;
            }
            default:
            {
                ReportUnsupported(command.Kind);
                break;
            }
        }
    }

    private void ReportUnsupported(PdfCommandKind kind)
    {
        if (_reportedUnsupportedKinds.Add(kind))
        {
            _logger.LogWarning("Snap does not draw {CommandKind} commands yet; they are skipped.", kind);
        }
    }

    private void RestoreTo(int saveCount)
    {
        while (_savedStates.Count > saveCount)
        {
            Restore();
        }
    }

    private void ExecuteDrawRecording(DrawRecordingCommand command)
    {
        int savesAfterOwnSave = BeginDrawRecording(command.TintPaint, command.Matrix);

        ReplayCommands(command.Recorder, savesAfterOwnSave);

        EndDrawRecording(savesAfterOwnSave);
    }

    /// <summary>
    /// Replays the appearance of the annotation's visual state in the execution context's annotation states, under the
    /// state's matrix, unless the state hides it.
    /// </summary>
    private void ExecuteDrawAnnotation(DrawAnnotationCommand command)
    {
        PdfAnnotationState state = _executionContext.AnnotationStates.GetState(command.Annotation);

        if (state.IsHidden)
        {
            return;
        }

        PdfCommandRecorder? recording = command.GetRecording(state.VisualState);

        if (recording == null)
        {
            return;
        }

        int savesAfterOwnSave = BeginDrawRecording(tintPaint: null, state.Matrix);

        ReplayCommands(recording, savesAfterOwnSave);

        EndDrawRecording(savesAfterOwnSave);
    }

    /// <summary>
    /// Replays the commands of <paramref name="recorder"/>; when the replay is cancelled, drops the text object the
    /// recording opened after <paramref name="savesAfterOwnSave"/> saves before passing the cancellation on.
    /// </summary>
    private void ReplayCommands(PdfCommandRecorder recorder, int savesAfterOwnSave)
    {
        try
        {
            foreach (IPdfCommand recordedCommand in recorder.Commands)
            {
                ExecuteCommand(recordedCommand);

                _executionContext.ExecutionObserver.Notify();
            }
        }
        catch (OperationCanceledException)
        {
            DiscardTextObject(savesAfterOwnSave);
            throw;
        }
    }

    /// <summary>
    /// Saves, takes the recording's tint and matrix, and returns the save count after that save.
    /// </summary>
    private int BeginDrawRecording(PdfPaint? tintPaint, in PdfMatrix matrix)
    {
        Save();

        if (tintPaint != null)
        {
            _state.TintPaint = tintPaint;
        }

        Concat(matrix);

        return _savedStates.Count;
    }

    /// <summary>
    /// Closes a text object the recording left open and restores to before the recording's own save.
    /// </summary>
    private void EndDrawRecording(int savesAfterOwnSave)
    {
        if (_textObject != null && _textObject.SaveCount >= savesAfterOwnSave)
        {
            CloseTextObject();
        }

        RestoreTo(savesAfterOwnSave);
        Restore();
    }
}
