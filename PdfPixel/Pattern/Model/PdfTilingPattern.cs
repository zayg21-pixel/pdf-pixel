using System;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Pattern.Utilities;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using SkiaSharp;

namespace PdfPixel.Pattern.Model;

/// <summary>
/// Represents a parsed tiling (/PatternType 1) pattern.
/// </summary>
public sealed class PdfTilingPattern : PdfPattern
{
    private readonly IPdfRenderer _renderer;

    internal PdfTilingPattern(
        IPdfRenderer renderer,
        PdfObject sourceObject,
        SKRect bbox,
        float xStep,
        float yStep,
        PdfTilingPaintType paintTypeKind,
        PdfTilingSpacingType tilingTypeKind,
        SKMatrix matrix)
        : base(sourceObject, matrix, PdfPatternType.Tiling)
    {
        _renderer = renderer;
        BBox = bbox;
        XStep = xStep;
        YStep = yStep;
        PaintTypeKind = paintTypeKind;
        TilingTypeKind = tilingTypeKind;
    }

    /// <summary>
    /// Gets the bounding box of the pattern cell.
    /// </summary>
    public SKRect BBox { get; }

    /// <summary>
    /// Gets the horizontal spacing between pattern cells.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    /// Gets the vertical spacing between pattern cells.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    /// Gets the paint type (colored or uncolored).
    /// </summary>
    public PdfTilingPaintType PaintTypeKind { get; }

    /// <summary>
    /// Gets the tiling type (spacing and distortion rules).
    /// </summary>
    public PdfTilingSpacingType TilingTypeKind { get; }

    internal override void RenderPattern(IPdfCommandProcessor processor, PdfGraphicsState state, IRenderTarget renderTarget)
    {
        PdfCommandRecorder? tileRecorder = TilingPatternShaderBuilder.RenderTilingCell(_renderer, this, state);

        if (tileRecorder == null)
        {
            return;
        }

        SKMatrix matrix = SKMatrix.Concat(state.CTM.Invert(), PatternMatrix);

        renderTarget.BeforePatternRender(processor);

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ConcatMatrixCommand(matrix));

        UncoloredPaintModifier? modifier = (PaintTypeKind == PdfTilingPaintType.Uncolored)
            ? new UncoloredPaintModifier(renderTarget.Color)
            : default;

        SKRect bounds = matrix.Invert().MapRect(renderTarget.Bounds);

        var startX = (float)(Math.Floor(bounds.Left / XStep) * XStep);
        var startY = (float)(Math.Floor(bounds.Top / YStep) * YStep);
        var endX = (float)(Math.Ceiling(bounds.Right / XStep) * XStep);
        var endY = (float)(Math.Ceiling(bounds.Bottom / YStep) * YStep);

        var xCount = (int)Math.Ceiling((endX - startX) / XStep);
        var yCount = (int)Math.Ceiling((endY - startY) / YStep);
        DrawRecordingCommand recordingCommand = new (tileRecorder, modifier);

        for (int i = 0; i <= xCount; i++)
        {
            float x = startX + (i * XStep);
            for (int j = 0; j <= yCount; j++)
            {
                float y = startY + (j * YStep);

                processor.Process(SaveStateCommand.Instance);
                processor.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(x, y)));
                processor.Process(recordingCommand);
                processor.Process(RestoreStateCommand.Instance);
            }
        }

        processor.Process(RestoreStateCommand.Instance);
        renderTarget.AfterPatternRender(processor);
    }
}
