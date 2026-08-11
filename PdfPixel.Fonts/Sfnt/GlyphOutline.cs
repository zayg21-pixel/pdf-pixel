using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A glyph's outline as "glyf" describes it, flattened: every point in the order that a component's
/// point-matching arguments number them, each point's flags, and the index of the last point of every
/// contour. Immutable - a composite glyph's components are combined through <see cref="Merge"/>,
/// which returns the combined outline instead of growing either of the two it was given.
/// </summary>
public sealed class GlyphOutline
{
    private readonly PdfFontPoint[] _points;
    private readonly byte[] _flags;
    private readonly int[] _endPoints;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphOutline"/> class. The arrays are taken as
    /// they are rather than copied, so a caller must not keep writing to them afterwards.
    /// </summary>
    /// <param name="points">Every point of the outline, in "glyf" order.</param>
    /// <param name="flags">Each point's flags, one per entry of <paramref name="points"/>.</param>
    /// <param name="endPoints">The index, into <paramref name="points"/>, of the last point of every contour.</param>
    /// <param name="instructions">The glyph's hinting instructions, empty when it has none.</param>
    public GlyphOutline(PdfFontPoint[] points, byte[] flags, int[] endPoints, in ReadOnlyMemory<byte> instructions)
    {
        if (points == null)
        {
            throw new ArgumentNullException(nameof(points));
        }

        if (flags == null)
        {
            throw new ArgumentNullException(nameof(flags));
        }

        if (endPoints == null)
        {
            throw new ArgumentNullException(nameof(endPoints));
        }

        _points = points;
        _flags = flags;
        _endPoints = endPoints;
        Instructions = instructions;
    }

    /// <summary>
    /// Gets an outline holding no points at all, which is where combining a composite glyph's
    /// components starts.
    /// </summary>
    public static GlyphOutline Empty { get; } = new(Array.Empty<PdfFontPoint>(), Array.Empty<byte>(), Array.Empty<int>(), default);

    /// <summary>
    /// Gets the glyph's hinting instructions, which address the point numbers this outline holds.
    /// Empty once the outline is assembled from components, since flattening renumbers the points
    /// those instructions were compiled against.
    /// </summary>
    public ReadOnlyMemory<byte> Instructions { get; }

    /// <summary>
    /// Gets every point of the outline, in the order that a component's point-matching arguments
    /// number them.
    /// </summary>
    public ReadOnlyMemory<PdfFontPoint> Points => _points;

    /// <summary>
    /// Gets each point's flags, one per entry of <see cref="Points"/>.
    /// </summary>
    public ReadOnlyMemory<byte> Flags => _flags;

    /// <summary>
    /// Gets the index, into <see cref="Points"/>, of the last point of every contour.
    /// </summary>
    public ReadOnlyMemory<int> EndPoints => _endPoints;

    /// <summary>
    /// Combines this outline with <paramref name="outline"/>, placing every point of the latter by
    /// <paramref name="transform"/> and shifting its contour ends past the points this one already
    /// holds, so the result numbers its points exactly as a composite glyph's point-matching
    /// arguments index them.
    /// </summary>
    /// <param name="outline">The outline whose points follow this one's.</param>
    /// <param name="transform">The transform placing <paramref name="outline"/>'s points.</param>
    /// <returns>
    /// The combined outline, keeping this outline's <see cref="Instructions"/> - its points keep the
    /// numbers they had, so what addressed them still does - and dropping <paramref name="outline"/>'s,
    /// whose points are renumbered past them. Neither operand is changed.
    /// </returns>
    public GlyphOutline Merge(GlyphOutline outline, in PdfFontMatrix transform)
    {
        if (outline == null)
        {
            throw new ArgumentNullException(nameof(outline));
        }

        int pointOffset = _points.Length;
        int contourOffset = _endPoints.Length;

        var points = new PdfFontPoint[pointOffset + outline._points.Length];
        var flags = new byte[_flags.Length + outline._flags.Length];
        var endPoints = new int[contourOffset + outline._endPoints.Length];

        _points.CopyTo(points, 0);
        for (int pointIndex = 0; pointIndex < outline._points.Length; pointIndex++)
        {
            points[pointOffset + pointIndex] = transform.MapPoint(outline._points[pointIndex]);
        }

        _flags.CopyTo(flags, 0);
        outline._flags.CopyTo(flags, _flags.Length);

        _endPoints.CopyTo(endPoints, 0);
        for (int contourIndex = 0; contourIndex < outline._endPoints.Length; contourIndex++)
        {
            endPoints[contourOffset + contourIndex] = outline._endPoints[contourIndex] + pointOffset;
        }

        return new GlyphOutline(points, flags, endPoints, Instructions);
    }
}
