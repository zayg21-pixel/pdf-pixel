using PdfPixel.Models;
using System;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Replays a recorded pattern cell across a tiled grid covering the given bounds.
/// </summary>
public sealed class DrawTilingCommand : PdfCommand
{
    /// <summary>
    /// Initializes the command with the pattern-space bounds to tile, the cell bounding box, the cell step, and the recorded cell content.
    /// </summary>
    public DrawTilingCommand(SKRect bounds, SKRect bbox, float xStep, float yStep, DrawRecordingCommand recordingCommand)
    {
        BBox = bbox;
        XStep = xStep;
        YStep = yStep;
        RecordingCommand = recordingCommand;

        var startX = (float)(Math.Floor(bounds.Left / xStep) * xStep);
        var startY = (float)(Math.Floor(bounds.Top / yStep) * yStep);
        var endX = (float)(Math.Ceiling(bounds.Right / xStep) * xStep);
        var endY = (float)(Math.Ceiling(bounds.Bottom / yStep) * yStep);

        TilingArea = new SKRect(startX, startY, endX, endY);
        XCount = (int)Math.Ceiling((endX - startX) / xStep);
        YCount = (int)Math.Ceiling((endY - startY) / yStep);
    }

    /// <summary>
    /// Gets the pattern cell's bounding box, in pattern space, that each tile is clipped to.
    /// </summary>
    public SKRect BBox { get; }

    /// <summary>
    /// Gets the pattern-space area to cover with tiles, expanded to whole steps.
    /// </summary>
    public SKRect TilingArea { get; }

    /// <summary>
    /// Gets the horizontal spacing between pattern cells.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    /// Gets the vertical spacing between pattern cells.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    /// Gets the number of tile columns to draw.
    /// </summary>
    public int XCount { get; }

    /// <summary>
    /// Gets the number of tile rows to draw.
    /// </summary>
    public int YCount { get; }

    /// <summary>
    /// Gets the recorded pattern cell replayed at each tile position.
    /// </summary>
    public DrawRecordingCommand RecordingCommand { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features => RecordingCommand.Features;

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        PdfCommandExecutionParameters childParameters = executionContext.Parameters.Clone();
        childParameters.SnapToDevicePixels = false;

        using SKPictureRecorder recorder = new();
        using SKCanvas canvas = recorder.BeginRecording(BBox);
        using PdfCommandExecutionContext childContext = new(
            childParameters,
            executionContext.ContentLocker,
            executionContext.OptionalContentGroups,
            executionContext.ExecutionObserver,
            canvas);

        // do not apply AA to clip BBox to avoid seams
        canvas.ClipRect(BBox, SKClipOperation.Intersect);
        childContext.Frames.OnConcatMatrix(executionContext.Frames.TotalMatrix);
        RecordingCommand.Execute(childContext);

        using SKPicture picture = recorder.EndRecording();

        //return;
        for (int i = 0; i <= XCount; i++)
        {
            float x = TilingArea.Left + (i * XStep);
            for (int j = 0; j <= YCount; j++)
            {
                float y = TilingArea.Top + (j * YStep);

                SKMatrix translation = SKMatrix.CreateTranslation(x, y);

                executionContext.Canvas.Save();
                executionContext.Canvas.Concat(translation);
                executionContext.Canvas.DrawPicture(picture);

                executionContext.Canvas.Restore();
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => RecordingCommand.Dispose();
}
