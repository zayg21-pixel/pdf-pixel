using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;

namespace PdfPixel.Commands.Context;

/// <summary>
/// Describes a single applied clip operation: a fill path, or a stroke path with the
/// paint whose stroke outline was clipped to.
/// </summary>
public sealed class PdfClipState
{
    private PdfClipState(PdfPath path, PdfClipOperation operation, PdfPaint? strokePaint)
    {
        Path = path;
        Operation = operation;
        StrokePaint = strokePaint;
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
    /// Creates a clip state for a fill clip path.
    /// </summary>
    public static PdfClipState ForPath(PdfPath path, PdfClipOperation operation) => new(path, operation, null);

    /// <summary>
    /// Creates a clip state for a stroke clip path, keeping the paint whose stroke outline was clipped to.
    /// </summary>
    public static PdfClipState ForStrokePath(PdfPath path, PdfClipOperation operation, PdfPaint paint) => new(path, operation, paint);
}
