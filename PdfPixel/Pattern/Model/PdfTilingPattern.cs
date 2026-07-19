using PdfPixel.Commands;
using PdfPixel.Geometry;
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
        in PdfRectangle bbox,
        float xStep,
        float yStep,
        PdfTilingPaintType paintTypeKind,
        PdfTilingSpacingType tilingTypeKind,
        in PdfMatrix matrix)
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
    public PdfRectangle BBox { get; }

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

        PdfMatrix matrix = PdfMatrix.Concat(state.CTM.Invert(), PatternMatrix);

        renderTarget.BeforePatternRender(processor);

        UncoloredPaintModifier? modifier = (PaintTypeKind == PdfTilingPaintType.Uncolored)
            ? new UncoloredPaintModifier(renderTarget.Color)
            : default;

        SKRect bounds = matrix.Invert().ToSkMatrix().MapRect(renderTarget.Bounds);

        DrawRecordingCommand recordingCommand = new(tileRecorder, modifier);
        processor.Process(new DrawTilingCommand(matrix.ToSkMatrix(), bounds, BBox.ToSkRect(), XStep, YStep, recordingCommand));

        renderTarget.AfterPatternRender(processor);
    }
}
