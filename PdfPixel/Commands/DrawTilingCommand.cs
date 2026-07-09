using System;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Replays a recorded pattern cell across a tiled grid covering the given bounds.
/// </summary>
public sealed class DrawTilingCommand : PdfCommand
{
    /// <summary>
    /// Initializes the command with the pattern-space bounds to tile, the cell step, and the recorded cell content.
    /// </summary>
    public DrawTilingCommand(SKRect bounds, float xStep, float yStep, DrawRecordingCommand recordingCommand)
    {
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
        for (int i = 0; i <= XCount; i++)
        {
            float x = TilingArea.Left + (i * XStep);
            for (int j = 0; j <= YCount; j++)
            {
                float y = TilingArea.Top + (j * YStep);

                SKMatrix translation = SKMatrix.CreateTranslation(x, y);

                executionContext.Canvas.Save();
                executionContext.Frames.OnSaveState();

                executionContext.Canvas.Concat(translation);
                executionContext.Frames.OnConcatMatrix(translation);

                RecordingCommand.Execute(executionContext);

                executionContext.Canvas.Restore();
                executionContext.Frames.OnRestoreState();
            }
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => RecordingCommand.Dispose();
}
