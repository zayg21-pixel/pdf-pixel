using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;
using System;

namespace PdfPixel.Commands.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteDrawTiling(DrawTilingCommand command)
    {
        _canvas.Save();
        _executionContext.Frames.OnSaveState();
        _canvas.Concat(command.Matrix.ToSkMatrix());
        _executionContext.Frames.OnConcatMatrix(command.Matrix);

        if (CommandHelpers.CanTileByRepeating(command, _executionContext))
        {
            // Cells sit at whole multiples of the step from the pattern space origin, so one step
            // starting at that origin is the unit a repeating shader covers the whole area with in a
            // single draw. The unit keeps the step exactly: rounding it to whole device pixels gives
            // the grid a period that changes with the zoom, drifting the pattern across the page and
            // leaving gaps between cells.
            SKRect tileUnit = new(0, 0, command.XStep, command.YStep);

            using SKPicture tile = RecordPatternTile(command, tileUnit);
            using SKShader shader = tile.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, tileUnit);
            using SKPaint shaderPaint = new() { Shader = shader };

            _canvas.DrawRect(command.TilingArea.ToSkRect(), shaderPaint);
        }
        else
        {
            using SKPicture cell = RecordPatternCell(command);

            for (int i = 0; i <= command.XCount; i++)
            {
                float x = command.TilingArea.Left + (i * command.XStep);
                for (int j = 0; j <= command.YCount; j++)
                {
                    float y = command.TilingArea.Top + (j * command.YStep);

                    SKMatrix translation = SKMatrix.CreateTranslation(x, y);

                    _canvas.Save();
                    _canvas.Concat(translation);
                    _canvas.DrawPicture(cell);

                    _canvas.Restore();
                }
            }
        }

        _canvas.Restore();
        _executionContext.Frames.OnRestoreState();
    }

    private SKPicture RecordPatternTile(DrawTilingCommand command, SKRect tileUnit)
    {
        SKRect skBBox = command.BBox.ToSkRect().Standardized;

        // A cell bigger than its step overlaps its neighbours, so every grid position reaching the
        // tile is recorded into it and the shader brings back from the next repeat what the cull
        // rectangle drops here.
        int firstColumn = (int)MathF.Floor((tileUnit.Left - skBBox.Right) / command.XStep) + 1;
        int lastColumn = (int)MathF.Ceiling((tileUnit.Right - skBBox.Left) / command.XStep) - 1;
        int firstRow = (int)MathF.Floor((tileUnit.Top - skBBox.Bottom) / command.YStep) + 1;
        int lastRow = (int)MathF.Ceiling((tileUnit.Bottom - skBBox.Top) / command.YStep) - 1;

        using SKPictureRecorder recorder = new();
        using SKCanvas canvas = recorder.BeginRecording(tileUnit);
        using PdfCommandExecutionContext childContext = CreateCellContext();

        SkCanvasCommandProcessor childProcessor = new(canvas, childContext, _logger);

        for (int column = firstColumn; column <= lastColumn; column++)
        {
            for (int row = firstRow; row <= lastRow; row++)
            {
                PdfMatrix translation = PdfMatrix.CreateTranslation(column * command.XStep, row * command.YStep);

                canvas.Save();
                childContext.Frames.OnSaveState();
                canvas.Concat(translation.ToSkMatrix());
                childContext.Frames.OnConcatMatrix(translation);

                // do not apply AA to clip BBox to avoid seams
                canvas.ClipRect(skBBox, SKClipOperation.Intersect);
                childProcessor.ExecuteDrawRecording(command.RecordingCommand);

                canvas.Restore();
                childContext.Frames.OnRestoreState();
            }
        }

        return recorder.EndRecording();
    }

    private SKPicture RecordPatternCell(DrawTilingCommand command)
    {
        SKRect skBBox = command.BBox.ToSkRect();

        using SKPictureRecorder recorder = new();
        using SKCanvas canvas = recorder.BeginRecording(skBBox);
        using PdfCommandExecutionContext childContext = CreateCellContext();

        // do not apply AA to clip BBox to avoid seams
        canvas.ClipRect(skBBox, SKClipOperation.Intersect);

        SkCanvasCommandProcessor childProcessor = new(canvas, childContext, _logger);
        childProcessor.ExecuteDrawRecording(command.RecordingCommand);

        return recorder.EndRecording();
    }

    private PdfCommandExecutionContext CreateCellContext()
    {
        PdfCommandExecutionParameters childParameters = _executionContext.Parameters.Clone();
        childParameters.SnapToDevicePixels = false;

        PdfCommandExecutionContext childContext = new(
            childParameters,
            _executionContext.ContentLocker,
            _executionContext.OptionalContentGroups,
            _executionContext.ExecutionObserver);

        childContext.Frames.OnConcatMatrix(_executionContext.Frames.TotalMatrix);

        return childContext;
    }
}
