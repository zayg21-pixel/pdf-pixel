using PdfPixel.Models;
using System;
using SkiaSharp;
using PdfPixel.Geometry;

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
    public DrawTilingCommand(in PdfMatrix matrix, in PdfRectangle bounds, in PdfRectangle bbox, float xStep, float yStep, DrawRecordingCommand recordingCommand)
    {
        Matrix = matrix;
        BBox = bbox;
        XStep = xStep;
        YStep = yStep;
        RecordingCommand = recordingCommand;

        float startX = MathF.Floor(bounds.Left / xStep) * xStep;
        float startY = MathF.Floor(bounds.Top / yStep) * yStep;
        float endX = MathF.Ceiling(bounds.Right / xStep) * xStep;
        float endY = MathF.Ceiling(bounds.Bottom / yStep) * yStep;

        TilingArea = new PdfRectangle(startX, startY, endX, endY);
        XCount = (int)MathF.Ceiling((endX - startX) / xStep);
        YCount = (int)MathF.Ceiling((endY - startY) / yStep);
    }

    /// <inheritdoc />
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the pattern cell's bounding box, in pattern space, that each tile is clipped to.
    /// </summary>
    public PdfRectangle BBox { get; }

    /// <summary>
    /// Gets the pattern-space area to cover with tiles, expanded to whole steps.
    /// </summary>
    public PdfRectangle TilingArea { get; }

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
        executionContext.Canvas.Concat(Matrix.ToSkMatrix());
        executionContext.Frames.OnConcatMatrix(Matrix);

        PdfCommandExecutionParameters childParameters = executionContext.Parameters.Clone();
        childParameters.SnapToDevicePixels = false;

        SKRect skBBox = BBox.ToSkRect();

        using (SKPictureRecorder recorder = new())
        {
            using SKCanvas canvas = recorder.BeginRecording(skBBox);
            using PdfCommandExecutionContext childContext = new(
                childParameters,
                executionContext.ContentLocker,
                executionContext.OptionalContentGroups,
                executionContext.ExecutionObserver,
                canvas);

            // do not apply AA to clip BBox to avoid seams
            canvas.ClipRect(skBBox, SKClipOperation.Intersect);
            childContext.Frames.OnConcatMatrix(executionContext.Frames.TotalMatrix);
            RecordingCommand.Execute(childContext);

            using SKPicture picture = recorder.EndRecording();

            ShaderTilingParameters shaderTilingParameters = GetShaderTilingParameters(executionContext);

            if (shaderTilingParameters.CanUseShaders)
            {
                SKRect tileRect = new(
                    skBBox.Left,
                    skBBox.Top,
                    skBBox.Left + shaderTilingParameters.ExactTileWidth,
                    skBBox.Top + shaderTilingParameters.ExactTileHeight);

                using SKShader shader = picture.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, tileRect);
                using SKPaint shaderPaint = new() { Shader = shader };

                executionContext.Canvas.DrawRect(TilingArea.ToSkRect(), shaderPaint);
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
        PdfMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(executionContext);
        PdfPoint deviceOrigin = deviceMatrix.MapPoint(PdfPoint.Empty);
        PdfPoint mappedX = deviceMatrix.MapPoint(new PdfPoint(XStep, 0));
        PdfPoint mappedY = deviceMatrix.MapPoint(new PdfPoint(0, YStep));

        float deviceXAxisX = mappedX.X - deviceOrigin.X;
        float deviceXAxisY = mappedX.Y - deviceOrigin.Y;
        float deviceYAxisX = mappedY.X - deviceOrigin.X;
        float deviceYAxisY = mappedY.Y - deviceOrigin.Y;

        float deviceXAxisLength = MathF.Sqrt((deviceXAxisX * deviceXAxisX) + (deviceXAxisY * deviceXAxisY));
        float deviceYAxisLength = MathF.Sqrt((deviceYAxisX * deviceYAxisX) + (deviceYAxisY * deviceYAxisY));

        float scaleX = deviceXAxisLength / XStep;
        float scaleY = deviceYAxisLength / YStep;

        float targetPixelsX = MathF.Round(deviceXAxisLength);
        float targetPixelsY = MathF.Round(deviceYAxisLength);

        float exactTileWidth = targetPixelsX / scaleX;
        float exactTileHeight = targetPixelsY / scaleY;

        int maxTileDeviceDimension = executionContext.Parameters.ImageTileSize;
        bool canUseShaders = deviceXAxisLength <= maxTileDeviceDimension && deviceYAxisLength <= maxTileDeviceDimension;

        return new ShaderTilingParameters(canUseShaders, exactTileWidth, exactTileHeight);
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawTilingCommand)} {CommandHelpers.FormatMatrix(Matrix)}";

    private readonly struct ShaderTilingParameters
    {
        public ShaderTilingParameters(bool canUseShaders, float exactTileWidth, float exactTileHeight)
        {
            CanUseShaders = canUseShaders;
            ExactTileWidth = exactTileWidth;
            ExactTileHeight = exactTileHeight;
        }

        public bool CanUseShaders { get; }

        public float ExactTileWidth { get; }

        public float ExactTileHeight { get; }
    }
}
