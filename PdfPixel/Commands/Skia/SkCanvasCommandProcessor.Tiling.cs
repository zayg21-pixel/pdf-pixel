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
            PdfRectangle tileUnit = new(0, 0, command.XStep, command.YStep);

            using SKPicture tile = RecordPatternTile(command, tileUnit);
            using SKShader shader = tile.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, tileUnit.ToSkRect());
            using SKPaint shaderPaint = new() { Shader = shader };

            _canvas.DrawRect(command.TilingArea.ToSkRect(), shaderPaint);
        }
        else
        {
            PdfIntegerRectangle grid = GetCellGrid(command, command.TilingArea);

            using SKPicture cell = RecordPatternCell(command);

            for (int column = grid.Left; column <= grid.Right; column++)
            {
                for (int row = grid.Top; row <= grid.Bottom; row++)
                {
                    SKMatrix translation = SKMatrix.CreateTranslation(column * command.XStep, row * command.YStep);

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

    private SKPicture RecordPatternTile(DrawTilingCommand command, in PdfRectangle tileUnit)
    {
        // A cell bigger than its step overlaps its neighbours, so every grid position reaching the
        // tile is recorded into it and the shader brings back from the next repeat what the cull
        // rectangle drops here.
        PdfIntegerRectangle grid = GetCellGrid(command, tileUnit);

        using SKPictureRecorder recorder = new();
        using SKCanvas canvas = recorder.BeginRecording(tileUnit.ToSkRect());
        using PdfCommandExecutionContext childContext = CreateCellContext();

        SkCanvasCommandProcessor childProcessor = new(canvas, childContext, _logger);

        for (int column = grid.Left; column <= grid.Right; column++)
        {
            for (int row = grid.Top; row <= grid.Bottom; row++)
            {
                PdfMatrix translation = PdfMatrix.CreateTranslation(column * command.XStep, row * command.YStep);

                canvas.Save();
                childContext.Frames.OnSaveState();
                canvas.Concat(translation.ToSkMatrix());
                childContext.Frames.OnConcatMatrix(translation);

                // do not apply AA to clip BBox to avoid seams
                canvas.ClipRect(command.BBox.ToSkRect(), SKClipOperation.Intersect);
                childProcessor.ExecuteDrawRecording(command.RecordingCommand);

                canvas.Restore();
                childContext.Frames.OnRestoreState();
            }
        }

        return recorder.EndRecording();
    }

    private SKPicture RecordPatternCell(DrawTilingCommand command)
    {
        using SKPictureRecorder recorder = new();
        using SKCanvas canvas = recorder.BeginRecording(command.BBox.ToSkRect());
        using PdfCommandExecutionContext childContext = CreateCellContext();

        // do not apply AA to clip BBox to avoid seams
        canvas.ClipRect(command.BBox.ToSkRect(), SKClipOperation.Intersect);

        SkCanvasCommandProcessor childProcessor = new(canvas, childContext, _logger);
        childProcessor.ExecuteDrawRecording(command.RecordingCommand);

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns the cells covering <paramref name="area"/> as inclusive cell indices, columns on the
    /// left and right edges and rows on the top and bottom. A cell sits at whole multiples of the
    /// step from the pattern space origin and belongs to the grid when its bounding box reaches into
    /// the area, so a cell wider than its step brings in the neighbours that overlap it. A step that
    /// does not advance leaves the single cell at the origin.
    /// </summary>
    private static PdfIntegerRectangle GetCellGrid(DrawTilingCommand command, in PdfRectangle area)
    {
        PdfRectangle bbox = command.BBox;

        int firstColumn = 0;
        int lastColumn = 0;

        if (command.XStep > 0)
        {
            firstColumn = (int)MathF.Floor((area.Left - bbox.Right) / command.XStep) + 1;
            lastColumn = (int)MathF.Ceiling((area.Right - bbox.Left) / command.XStep) - 1;
        }

        int firstRow = 0;
        int lastRow = 0;

        if (command.YStep > 0)
        {
            firstRow = (int)MathF.Floor((area.Top - bbox.Bottom) / command.YStep) + 1;
            lastRow = (int)MathF.Ceiling((area.Bottom - bbox.Top) / command.YStep) - 1;
        }

        return new PdfIntegerRectangle(firstColumn, firstRow, lastColumn, lastRow);
    }

    private PdfCommandExecutionContext CreateCellContext()
    {
        PdfCommandExecutionParameters childParameters = _executionContext.Parameters.Clone();
        childParameters.SnapToDevicePixels = false;

        PdfCommandExecutionContext childContext = new(
            _executionContext.Document,
            childParameters,
            _executionContext.ContentLocker,
            _executionContext.OptionalContentGroups,
            _executionContext.ExecutionObserver);

        childContext.Frames.OnConcatMatrix(_executionContext.Frames.TotalMatrix);

        return childContext;
    }
}
