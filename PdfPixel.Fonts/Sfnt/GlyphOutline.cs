using PdfPixel.Fonts.Model;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// A glyph's outline as "glyf" describes it, flattened: every point in the order that a component's
/// point-matching arguments number them, each point's flags, and the index of the last point of every
/// contour. Immutable - a composite glyph's components are combined through <see cref="Merge"/>,
/// which returns the combined outline instead of growing either of the two it was given.
/// </summary>
/// <remarks>
/// Only a glyph read for its path is ever flattened this way. Writing "glyf" back out keeps a
/// composite composite, so nothing on that path places a component or leaves the design grid.
/// </remarks>
public sealed class GlyphOutline
{
    private readonly GlyphPoint[] _points;
    private readonly byte[] _flags;
    private readonly int[] _endPoints;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlyphOutline"/> class. The arrays are taken as
    /// they are rather than copied, so a caller must not keep writing to them afterwards.
    /// </summary>
    /// <param name="points">Every point of the outline, in "glyf" order.</param>
    /// <param name="flags">Each point's flags, one per entry of <paramref name="points"/>.</param>
    /// <param name="endPoints">The index, into <paramref name="points"/>, of the last point of every contour.</param>
    public GlyphOutline(GlyphPoint[] points, byte[] flags, int[] endPoints)
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
    }

    /// <summary>
    /// Gets an outline holding no points at all, which is where combining a composite glyph's
    /// components starts.
    /// </summary>
    public static GlyphOutline Empty { get; } = new([], [], []);

    /// <summary>
    /// Gets every point of the outline, in the order that a component's point-matching arguments
    /// number them.
    /// </summary>
    public ReadOnlySpan<GlyphPoint> Points => _points;

    /// <summary>
    /// Gets each point's flags, one per entry of <see cref="Points"/>.
    /// </summary>
    public ReadOnlySpan<byte> Flags => _flags;

    /// <summary>
    /// Gets the index, into <see cref="Points"/>, of the last point of every contour.
    /// </summary>
    public ReadOnlySpan<int> EndPoints => _endPoints;

    /// <summary>
    /// Places one of a component's points by the transform its record carries, back onto the whole
    /// design units the rest of the outline sits on. A component that only offsets its glyph - which
    /// nearly all of them do - never leaves those units in the first place.
    /// </summary>
    /// <param name="point">The point to place.</param>
    /// <param name="transform">The transform placing it.</param>
    public static GlyphPoint Place(in GlyphPoint point, in PdfFontMatrix transform)
    {
        if (transform.ScaleX == 1f && transform.ScaleY == 1f && transform.SkewX == 0f && transform.SkewY == 0f)
        {
            return GlyphPoint.Clamped(point.X + (int)transform.TransX, point.Y + (int)transform.TransY);
        }

        float x = (transform.ScaleX * point.X) + (transform.SkewX * point.Y) + transform.TransX;
        float y = (transform.SkewY * point.X) + (transform.ScaleY * point.Y) + transform.TransY;

        return GlyphPoint.Clamped(Round(x), Round(y));
    }

    /// <summary>
    /// Combines this outline with <paramref name="outline"/>, placing every point of the latter by
    /// <paramref name="transform"/> and shifting its contour ends past the points this one already
    /// holds, so the result numbers its points exactly as a composite glyph's point-matching
    /// arguments index them.
    /// </summary>
    /// <param name="outline">The outline whose points follow this one's.</param>
    /// <param name="transform">The transform placing <paramref name="outline"/>'s points.</param>
    /// <returns>
    /// Resulted <see cref="GlyphOutline"/>.
    /// </returns>
    public GlyphOutline Merge(GlyphOutline outline, in PdfFontMatrix transform)
    {
        if (outline == null)
        {
            throw new ArgumentNullException(nameof(outline));
        }

        int pointOffset = _points.Length;
        int contourOffset = _endPoints.Length;

        var points = new GlyphPoint[pointOffset + outline._points.Length];
        var flags = new byte[_flags.Length + outline._flags.Length];
        var endPoints = new int[contourOffset + outline._endPoints.Length];

        _points.CopyTo(points, 0);
        for (int pointIndex = 0; pointIndex < outline._points.Length; pointIndex++)
        {
            points[pointOffset + pointIndex] = Place(outline._points[pointIndex], transform);
        }

        _flags.CopyTo(flags, 0);
        outline._flags.CopyTo(flags, _flags.Length);

        _endPoints.CopyTo(endPoints, 0);
        for (int contourIndex = 0; contourIndex < outline._endPoints.Length; contourIndex++)
        {
            endPoints[contourOffset + contourIndex] = outline._endPoints[contourIndex] + pointOffset;
        }

        return new GlyphOutline(points, flags, endPoints);
    }

    private static int Round(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
}
