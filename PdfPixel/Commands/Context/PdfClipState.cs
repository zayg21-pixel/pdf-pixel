using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;

namespace PdfPixel.Commands.Context;

/// <summary>
/// Describes a single applied clip operation: a fill path, or a stroke path with the
/// paint whose stroke outline was clipped to. Linked to the clip in effect before it through <see cref="Parent"/>.
/// </summary>
public sealed class PdfClipState
{
    private PdfClipState(PdfPath path, PdfClipOperation operation, PdfPaint? strokePaint, in PdfMatrix matrix, PdfClipState? parent)
    {
        Path = path;
        Operation = operation;
        StrokePaint = strokePaint;
        Matrix = matrix;
        Parent = parent;
    }

    /// <summary>
    /// Gets the clipped path.
    /// </summary>
    public PdfPath Path { get; }

    /// <summary>
    /// Gets the clip operation applied.
    /// </summary>
    public PdfClipOperation Operation { get; }

    /// <summary>
    /// Gets the paint whose stroke outline <see cref="Path"/> was clipped to; null unless this clip
    /// is stroke-path-based.
    /// </summary>
    public PdfPaint? StrokePaint { get; }

    /// <summary>
    /// Gets the total transformation matrix <see cref="Path"/> was clipped under.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the clip in effect before this one, or null when this is the first clip.
    /// </summary>
    public PdfClipState? Parent { get; }

    /// <summary>
    /// Creates a clip state for a fill clip path.
    /// </summary>
    public static PdfClipState ForPath(PdfPath path, PdfClipOperation operation, in PdfMatrix matrix, PdfClipState? parent)
        => new(path, operation, null, matrix, parent);

    /// <summary>
    /// Creates a clip state for a stroke clip path, keeping the paint whose stroke outline was clipped to.
    /// </summary>
    public static PdfClipState ForStrokePath(PdfPath path, PdfClipOperation operation, PdfPaint paint, in PdfMatrix matrix, PdfClipState? parent)
        => new(path, operation, paint, matrix, parent);
}
