using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using SkiaSharp;
using System;

namespace PdfPixel.Skia;

/// <summary>
/// The Skia rendering backend: draws recorded PDF commands onto an <see cref="SKCanvas"/>.
/// </summary>
public sealed partial class SkCanvasCommandProcessor : IPdfCommandProcessor
{
    private readonly SKCanvas _canvas;
    private readonly PdfCommandExecutionContext _executionContext;
    private readonly ILogger<SkCanvasCommandProcessor> _logger;

    /// <summary>
    /// Initializes the processor with the canvas to draw on, the execution context to draw with, and a logger.
    /// </summary>
    public SkCanvasCommandProcessor(SKCanvas canvas, PdfCommandExecutionContext executionContext, ILogger<SkCanvasCommandProcessor> logger)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Process(IPdfCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        _executionContext.ExecutionObserver.Notify();

        if (!_executionContext.MarkedContent.ShouldExecute(command))
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
            case PdfCommandKind.BeginMarkedContent:
            {
                _executionContext.MarkedContent.Push(((BeginMarkedContentCommand)command).MarkedContent);
                break;
            }
            case PdfCommandKind.ClipPath:
            {
                ExecuteClipPath((ClipPathCommand)command);
                break;
            }
            case PdfCommandKind.ClipRectangle:
            {
                ExecuteClipRectangle((ClipRectangleCommand)command);
                break;
            }
            case PdfCommandKind.ConcatMatrix:
            {
                var concatMatrixCommand = (ConcatMatrixCommand)command;
                _canvas.Concat(concatMatrixCommand.Matrix.ToSkMatrix());
                _executionContext.Frames.OnConcatMatrix(concatMatrixCommand.Matrix);
                break;
            }
            case PdfCommandKind.DrawNormalImageTile:
            {
                ExecuteDrawNormalImageTile((DrawNormalImageTileCommand)command);
                break;
            }
            case PdfCommandKind.DrawPath:
            {
                ExecuteDrawPath((DrawPathCommand)command);
                break;
            }
            case PdfCommandKind.DrawRecording:
            {
                ExecuteDrawRecording((DrawRecordingCommand)command);
                break;
            }
            case PdfCommandKind.DrawShading:
            {
                ExecuteDrawShading((DrawShadingCommand)command);
                break;
            }
            case PdfCommandKind.DrawShapedText:
            {
                ExecuteDrawShapedText((DrawShapedTextCommand)command);
                break;
            }
            case PdfCommandKind.DrawSoftMaskImageTile:
            {
                ExecuteDrawSoftMaskImageTile((DrawSoftMaskImageTileCommand)command);
                break;
            }
            case PdfCommandKind.DrawStencilMaskImageTile:
            {
                ExecuteDrawStencilMaskImageTile((DrawStencilMaskImageTileCommand)command);
                break;
            }
            case PdfCommandKind.DrawTiling:
            {
                ExecuteDrawTiling((DrawTilingCommand)command);
                break;
            }
            case PdfCommandKind.EndMarkedContent:
            {
                _executionContext.MarkedContent.Pop();
                break;
            }
            case PdfCommandKind.InitializeTileCache:
            {
                ExecuteInitializeTileCache((InitializeTileCacheCommand)command);
                break;
            }
            case PdfCommandKind.RestoreLayer:
            {
                _canvas.Restore();
                _executionContext.Frames.OnRestoreState();
                break;
            }
            case PdfCommandKind.RestoreState:
            {
                _canvas.Restore();
                _executionContext.Frames.OnRestoreState();
                break;
            }
            case PdfCommandKind.SaveLayer:
            {
                ExecuteSaveLayer((SaveLayerCommand)command);
                break;
            }
            case PdfCommandKind.SaveState:
            {
                _canvas.Save();
                _executionContext.Frames.OnSaveState();
                break;
            }
            case PdfCommandKind.TextCharacters:
            {
                ExecuteTextCharacters((TextCharactersCommand)command);
                break;
            }
        }
    }

    private void ExecuteDrawRecording(DrawRecordingCommand command)
    {
        UncoloredPaintModifier? previousModifier = _executionContext.UncoloredModifier;

        if (command.Modifier != null)
        {
            _executionContext.UncoloredModifier = command.Modifier;
        }

        _canvas.Save();
        _executionContext.Frames.OnSaveState();
        _canvas.Concat(command.Matrix.ToSkMatrix());
        _executionContext.Frames.OnConcatMatrix(command.Matrix);

        int savesCountAfterOwnSave = _executionContext.Frames.SavesCount;

        ReplayRecorder(command.Recorder);

        BalanceFrames(savesCountAfterOwnSave);

        _canvas.Restore();
        _executionContext.Frames.OnRestoreState();

        _executionContext.UncoloredModifier = previousModifier;
    }

    private void ReplayRecorder(PdfCommandRecorder recorder)
    {
        foreach (IPdfCommand recordedCommand in recorder.Commands)
        {
            if (!_executionContext.MarkedContent.ShouldExecute(recordedCommand))
            {
                continue;
            }

            try
            {
                ProcessInternal(recordedCommand);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogWarning(exception, "Command {CommandType} failed during replay.", recordedCommand.GetType().Name);
                continue;
            }

            _executionContext.ExecutionObserver?.Notify();
        }
    }

    private void BalanceFrames(int savesCountBefore)
    {
        PdfCommandExecutionFrames frames = _executionContext.Frames;

        while (frames.SavesCount > savesCountBefore)
        {
            _canvas.Restore();
            frames.OnRestoreState();
        }

        while (frames.SavesCount < savesCountBefore)
        {
            _canvas.Save();
            frames.OnSaveState();
        }
    }

    private void ExecuteSaveLayer(SaveLayerCommand command)
    {
        SKRect skBounds = command.Bounds.ToSkRect();

        if (command.Paint != null)
        {
            using SKPaint paint = command.Paint.ToSkiaPaint();
            paint.IsAntialias = _executionContext.Parameters.Antialias;
            _canvas.SaveLayer(skBounds, paint);
        }
        else
        {
            _canvas.SaveLayer(skBounds, null);
        }

        _executionContext.Frames.OnSaveLayer();
    }
}
