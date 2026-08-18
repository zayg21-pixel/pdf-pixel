using PdfPixel.Commands;
using PdfPixel.Commands.Cache;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Skia.Cache;
using SkiaSharp;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteDrawTiling(DrawTilingCommand command)
    {
        _canvas.Save();
        _executionContext.Frames.OnSaveState();
        _canvas.Concat(command.Matrix.ToSkMatrix());
        _executionContext.Frames.OnConcatMatrix(command.Matrix);

        // Taken after the command's own matrix is in, so it carries everything that shapes the
        // recorded picture and tells apart two uses of the pattern that do not.
        PdfMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(_executionContext);

        if (CommandHelpers.CanTileByRepeating(command, _executionContext))
        {
            TilingCommandCacheEntry entry = GetOrBuildTile(command, deviceMatrix);

            if (entry.Shader != null)
            {
                using SKPaint shaderPaint = new() { Shader = entry.Shader };

                _canvas.DrawRect(command.TilingArea.ToSkRect(), shaderPaint);
            }
        }
        else
        {
            PdfIntegerRectangle grid = command.CellGrid;

            TilingCommandCacheEntry entry = GetOrBuildCell(command, deviceMatrix);

            for (int column = grid.Left; column <= grid.Right; column++)
            {
                for (int row = grid.Top; row <= grid.Bottom; row++)
                {
                    SKMatrix translation = SKMatrix.CreateTranslation(column * command.XStep, row * command.YStep);

                    _canvas.Save();
                    _canvas.Concat(translation);
                    _canvas.DrawPicture(entry.Picture);

                    _canvas.Restore();
                }
            }
        }

        _canvas.Restore();
        _executionContext.Frames.OnRestoreState();
    }

    /// <summary>
    /// Returns the cached tile recorded for the pattern, recording and storing one when the cache
    /// holds none for this cell, tint, and device matrix.
    /// </summary>
    private TilingCommandCacheEntry GetOrBuildTile(DrawTilingCommand command, in PdfMatrix deviceMatrix)
    {
        TilingCommandCacheKey key = new(command, deviceMatrix, repeating: true);

        if (_executionContext.Cache.GetEntry(key) is TilingCommandCacheEntry existing)
        {
            return existing;
        }

        PdfRectangle tileUnit = new(0, 0, command.XStep, command.YStep);

        PdfSize deviceStep = CommandHelpers.GetDeviceStepSize(command, _executionContext);
        float tileScaleX = deviceStep.Width / command.XStep;
        float tileScaleY = deviceStep.Height / command.YStep;
        SKRect deviceTile = new(0, 0, deviceStep.Width, deviceStep.Height);
        SKMatrix tileMatrix = SKMatrix.CreateScale(1f / tileScaleX, 1f / tileScaleY);

        SKPicture tile = RecordPatternTile(command, tileUnit, tileScaleX, tileScaleY, deviceTile);
        SKShader shader = tile.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, tileMatrix, deviceTile);

        TilingCommandCacheEntry entry = new(tile, shader);
        _executionContext.Cache.StoreEntry(key, entry);
        return entry;
    }

    /// <summary>
    /// Returns the cached cell recorded for the pattern, recording and storing one when the cache
    /// holds none for this cell, tint, and device matrix.
    /// </summary>
    private TilingCommandCacheEntry GetOrBuildCell(DrawTilingCommand command, in PdfMatrix deviceMatrix)
    {
        TilingCommandCacheKey key = new(command, deviceMatrix, repeating: false);

        if (_executionContext.Cache.GetEntry(key) is TilingCommandCacheEntry existing)
        {
            return existing;
        }

        TilingCommandCacheEntry entry = new(RecordPatternCell(command), null);
        _executionContext.Cache.StoreEntry(key, entry);
        return entry;
    }

    private SKPicture RecordPatternTile(DrawTilingCommand command, in PdfRectangle tileUnit, float tileScaleX, float tileScaleY, SKRect deviceTile)
    {
        // A cell bigger than its step overlaps its neighbours, so every grid position reaching the
        // tile is recorded into it and the shader brings back from the next repeat what the cull
        // rectangle drops here.
        PdfIntegerRectangle grid = command.GetCellGrid(tileUnit);

        using SKPictureRecorder recorder = new();
        using SKCanvas canvas = recorder.BeginRecording(deviceTile);
        using PdfCommandExecutionContext childContext = CreateCellContext();

        // The shader's local matrix undoes this, so it never reaches the frames.
        canvas.Scale(tileScaleX, tileScaleY);

        SkCanvasCommandProcessor childProcessor = new(canvas, childContext, _fontSubstitutor, _logger);

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

        SkCanvasCommandProcessor childProcessor = new(canvas, childContext, _fontSubstitutor, _logger);
        childProcessor.ExecuteDrawRecording(command.RecordingCommand);

        return recorder.EndRecording();
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
