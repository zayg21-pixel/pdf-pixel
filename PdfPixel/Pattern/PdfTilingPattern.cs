using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Rendering;
using PdfPixel.Streams;
using PdfPixel.Transparency;

namespace PdfPixel.Pattern;

/// <summary>
/// Represents a parsed tiling (/PatternType 1) pattern.
/// </summary>
public sealed class PdfTilingPattern : PdfPattern
{
    private static readonly PdfTransparencyGroupContext CellGroup = new(null, isolated: false, knockout: false);

    internal PdfTilingPattern(
        in PdfReference sourceReference,
        PdfObjectStream sourceStream,
        PdfDictionary? resources,
        in PdfRectangle boundingBox,
        float xStep,
        float yStep,
        PdfTilingPaintType? paintType,
        PdfTilingType? tilingType,
        in PdfMatrix matrix)
        : base(matrix, PdfPatternType.Tiling)
    {
        SourceReference = sourceReference;
        SourceStream = sourceStream;
        Resources = resources;
        BoundingBox = boundingBox;
        XStep = xStep;
        YStep = yStep;
        PaintType = paintType;
        TilingType = tilingType;
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
    public PdfDictionary? Resources { get; }

    /// <summary>
    /// Gets the bounding box of the pattern cell.
    /// </summary>
    public PdfRectangle BoundingBox { get; }

    /// <summary>
    /// Gets the horizontal spacing between pattern cells.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    /// Gets the vertical spacing between pattern cells.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    /// Gets the paint type (colored or uncolored), or null when /PaintType is absent or not a defined value.
    /// </summary>
    public PdfTilingPaintType? PaintType { get; }

    /// <summary>
    /// Gets the tiling type (spacing and distortion rules), or null when /TilingType is absent or not a defined value.
    /// </summary>
    public PdfTilingType? TilingType { get; }

    /// <summary>
    /// The recorded cell, or null until one has been recorded.
    /// </summary>
    internal PdfCommandRecorder? CellRecording { get; set; }

    internal override PdfGroupSource? GetGroupSource(PdfGraphicsState state, PdfPaint paint)
    {
        PdfCommandRecorder? tileRecorder = CellRecording;

        if (tileRecorder == null)
        {
            // Anything already under way is suppressed wherever the cell reaches it again, which makes
            // the recording specific to this use; only a cell reached with nothing else in flight holds
            // for every other use of the pattern.
            bool reachedAtTopLevel = state.RecursionGuard.Count == 0;

            tileRecorder = TilingPatternCellRenderer.RenderTilingCell(this, state);

            if (tileRecorder != null && reachedAtTopLevel)
            {
                CellRecording = tileRecorder;
            }
        }

        if (tileRecorder == null)
        {
            return null;
        }

        PdfMatrix matrix = PdfMatrix.Concat(state.CTM.Invert(), Matrix);

        PdfPaint? tintPaint = (PaintType == PdfTilingPaintType.Uncolored)
            ? paint
            : null;

        DrawRecordingCommand recordingCommand = new(tileRecorder, matrix, tintPaint);

        return new PdfGroupSource(recordingCommand, CellGroup, null, new PdfTiling(BoundingBox, XStep, YStep));
    }
}
