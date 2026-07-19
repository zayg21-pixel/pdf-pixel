using PdfPixel.Models;
using System;
using SkiaSharp;
using PdfPixel.Color.Paint;

namespace PdfPixel.Commands;

/// <summary>
/// Replays a recorded pattern cell across a tiled grid covering the given bounds, under its own matrix.
/// Owns a save/restore around the replay because it executes a nested <see cref="DrawRecordingCommand"/>
/// and draws directly onto the canvas in pattern space.
/// </summary>
public sealed class DrawTilingCommand : PdfCommand, IMatrixCommand
{
    /// <summary>
    /// Initializes the command with the matrix, the pattern-space bounds to tile, the cell bounding box, the cell step, and the recorded cell content.
    /// </summary>
    public DrawTilingCommand(SKMatrix matrix, SKRect bounds, SKRect bbox, float xStep, float yStep, DrawRecordingCommand recordingCommand)
    {
        Matrix = matrix;
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
    /// Initializes the command with the matrix, the pattern-space bounds to tile, the cell bounding box, the cell step, and the recorded cell content.
    /// </summary>
    public DrawTilingCommand(in PdfMatrix matrix, in PdfRectangle bounds, in PdfRectangle bbox, float xStep, float yStep, DrawRecordingCommand recordingCommand)
        : this(matrix.ToSkMatrix(), bounds.ToSkRect(), bbox.ToSkRect(), xStep, yStep, recordingCommand)
    {
    }

    /// <inheritdoc />
    public SKMatrix Matrix { get; }

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
        executionContext.Canvas.Save();
        executionContext.Frames.OnSaveState();
        executionContext.Canvas.Concat(Matrix);
        executionContext.Frames.OnConcatMatrix(Matrix);

        PdfCommandExecutionParameters childParameters = executionContext.Parameters.Clone();
        childParameters.SnapToDevicePixels = false;

        using (SKPictureRecorder recorder = new())
        {
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

            ShaderTilingParameters shaderTilingParameters = GetShaderTilingParameters(executionContext);

            if (shaderTilingParameters.CanUseShaders)
            {
                SKRect tileRect = new(
                    BBox.Left,
                    BBox.Top,
                    BBox.Left + shaderTilingParameters.ExactTileSize.Width,
                    BBox.Top + shaderTilingParameters.ExactTileSize.Height);

                using SKShader shader = picture.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, tileRect);
                using SKPaint shaderPaint = PdfPaintFactory.CreateShaderPaint();
                shaderPaint.Shader = shader;

                executionContext.Canvas.DrawRect(TilingArea, shaderPaint);
            }
            else
            {
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
        }

        executionContext.Canvas.Restore();
        executionContext.Frames.OnRestoreState();
    }

    private ShaderTilingParameters GetShaderTilingParameters(PdfCommandExecutionContext executionContext)
    {
        SKMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(executionContext);
        SKPoint deviceOrigin = deviceMatrix.MapPoint(SKPoint.Empty);
        SKPoint deviceXAxis = deviceMatrix.MapPoint(new SKPoint(XStep, 0)) - deviceOrigin;
        SKPoint deviceYAxis = deviceMatrix.MapPoint(new SKPoint(0, YStep)) - deviceOrigin;

        float scaleX = deviceXAxis.Length / XStep;
        float scaleY = deviceYAxis.Length / YStep;

        float targetPixelsX = MathF.Round(deviceXAxis.Length);
        float targetPixelsY = MathF.Round(deviceYAxis.Length);

        SKSize exactTileSize = new(targetPixelsX / scaleX, targetPixelsY / scaleY);

        int maxTileDeviceDimension = executionContext.Parameters.ImageTileSize;
        bool canUseShaders = deviceXAxis.Length <= maxTileDeviceDimension && deviceYAxis.Length <= maxTileDeviceDimension;

        return new ShaderTilingParameters(canUseShaders, exactTileSize);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing) => RecordingCommand.Dispose();

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawTilingCommand)} {CommandHelpers.FormatMatrix(Matrix)}";

    private readonly struct ShaderTilingParameters
    {
        public ShaderTilingParameters(bool canUseShaders, SKSize exactTileSize)
        {
            CanUseShaders = canUseShaders;
            ExactTileSize = exactTileSize;
        }

        public bool CanUseShaders { get; }

        public SKSize ExactTileSize { get; }
    }
}
