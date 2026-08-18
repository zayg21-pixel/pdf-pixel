using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Pattern.Utilities;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Streams;

namespace PdfPixel.Pattern.Model;

/// <summary>
/// Represents a parsed tiling (/PatternType 1) pattern.
/// </summary>
public sealed class PdfTilingPattern : PdfPattern
{
    internal PdfTilingPattern(
        in PdfReference sourceReference,
        PdfObjectStream sourceStream,
        PdfDictionary? cellResources,
        in PdfRectangle bbox,
        float xStep,
        float yStep,
        PdfTilingPaintType paintTypeKind,
        PdfTilingSpacingType tilingTypeKind,
        in PdfMatrix matrix)
        : base(matrix, PdfPatternType.Tiling)
    {
        SourceReference = sourceReference;
        SourceStream = sourceStream;
        CellResources = cellResources;
        BBox = bbox;
        XStep = xStep;
        YStep = yStep;
        PaintTypeKind = paintTypeKind;
        TilingTypeKind = tilingTypeKind;
    }

    /// <summary>
    /// Reference of the pattern stream object, under which the recorded cell is cached. A tiling pattern
    /// is a stream, so it is always an indirect object.
    /// </summary>
    public PdfReference SourceReference { get; }

    /// <summary>
    /// The pattern's content stream, holding the operators that paint one cell.
    /// </summary>
    public PdfObjectStream SourceStream { get; }

    /// <summary>
    /// The pattern's own /Resources, which the cell's content stream resolves its names against, or
    /// null when the pattern declares none.
    /// </summary>
    public PdfDictionary? CellResources { get; }

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

    /// <summary>
    /// The recorded cell, or null until one has been recorded.
    /// </summary>
    internal PdfCommandRecorder? CellRecording { get; set; }

    /// <inheritdoc />
    internal override bool IsPageIndependent => true;

    internal override void RenderPattern(IPdfCommandProcessor processor, PdfGraphicsState state, IRenderTarget renderTarget)
    {
        PdfCommandRecorder? tileRecorder = CellRecording;

        if (tileRecorder == null)
        {
            // Anything already under way is suppressed wherever the cell reaches it again, which makes
            // the recording specific to this use; only a cell reached with nothing else in flight holds
            // for every other use of the pattern.
            bool reachedAtTopLevel = state.RecursionGuard.Count == 0;

            tileRecorder = TilingPatternShaderBuilder.RenderTilingCell(this, state);

            if (tileRecorder != null
                && SourceReference.IsValid
                && reachedAtTopLevel)
            {
                CellRecording = tileRecorder;
            }
        }

        if (tileRecorder == null)
        {
            return;
        }

        PdfMatrix matrix = PdfMatrix.Concat(state.CTM.Invert(), PatternMatrix);

        UncoloredPaintModifier? modifier = (PaintTypeKind == PdfTilingPaintType.Uncolored)
            ? new UncoloredPaintModifier(renderTarget.Color)
            : default;

        PdfRectangle bounds = matrix.Invert().MapRect(renderTarget.Bounds);

        DrawRecordingCommand recordingCommand = new(tileRecorder, modifier);
        DrawTilingCommand tilingCommand = new(matrix, bounds, BBox, XStep, YStep, recordingCommand);

        renderTarget.BeforePatternRender(processor, matrix.MapRect(tilingCommand.PaintedArea));
        processor.Process(tilingCommand);

        renderTarget.AfterPatternRender(processor);
    }
}
