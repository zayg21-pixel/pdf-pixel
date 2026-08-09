using PdfPixel.Color.Paint;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace PdfPixel.Geometry;

/// <summary>
/// Builds the fill outline of a stroked <see cref="PdfPath"/> directly in <see cref="PdfPath"/> geometry,
/// without going through Skia. Supports caps, joins, and dash patterns. Does not support
/// <see cref="PdfStrokeEffectType.Cloudy"/> (the decorative border-bump effect is out of scope; a plain
/// stroke outline is produced regardless of <see cref="PdfStrokeStyle.EffectType"/>).
/// </summary>
internal static partial class PdfStrokeOutlineBuilder
{
    // How far the emitted offset curve may stray from the true offset, measured at the segment midpoint
    // and in device units, before the cubic is split and its halves offset separately.
    private const float DeviceOffsetTolerance = 0.1f;

    // Every level of subdivision doubles the cubics a source curve can turn into, so this bounds one
    // curve's offset rail at two to the power of it.
    private const int MaxSubdivisionDepth = 6;

    // A dash pattern that would cut a sub-path into more pieces than this leaves it solid instead.
    private const int MaxDashPieces = 10000;

    private const float Epsilon = 1e-4f;

    // A cubic's length is measured by sampling it at even steps in t and summing the chords between the
    // samples. How far apart those samples are meant to fall is expressed in the space the path is built
    // in, which is the space a dash pattern is written in too, so the error stays fixed against the dash
    // lengths it has to land between rather than against the size of the curve.
    private const float CurveMeasureStep = 0.25f;
    private const int MinCurveMeasureSamples = 8;
    private const int MaxCurveMeasureSamples = 256;

    private const int OutlineCapacityFactor = 2;

    /// <summary>
    /// Builds the fill outline of <paramref name="source"/> stroked with the pen <paramref name="penMatrix"/>
    /// shapes: the circle of the line width in <paramref name="style"/> put through that matrix, which is an
    /// ellipse for a matrix scaling the two axes differently. The outline comes back in the space
    /// <paramref name="source"/> is given in, and the dash pattern is measured along the path in that same
    /// space. <paramref name="deviceMatrix"/> is what carries that space to the device, and the curve
    /// flattening is held to a tolerance measured there rather than one read as a distance in the space the
    /// path happens to be written in.
    /// </summary>
    public static PdfPath BuildOutline(PdfPath source, PdfStrokeStyle style, in PdfMatrix penMatrix, in PdfMatrix deviceMatrix)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (style == null)
        {
            throw new ArgumentNullException(nameof(style));
        }

        float halfWidth = MathF.Max(style.LineWidth, 0f) / 2f;
        if (halfWidth <= 0f)
        {
            halfWidth = 0.5f;
        }

        // The offsetting emits its points in pen space, which reaches the device through the pen matrix and
        // then the device matrix, so the tolerance travels the same way back to land on the device.
        float offsetTolerance = GetOffsetTolerance(penMatrix.PostConcat(deviceMatrix));

        // The offsetting runs where the pen is a circle; the builder carries each emitted point back.
        PdfMatrix toPenSpace = penMatrix.Invert();
        PdfPathBuilder result = new(GetOutlineCapacity(source, halfWidth, offsetTolerance), penMatrix);

        float[]? dashPattern = GetDashPattern(style);

        foreach (SubPath subPath in EnumerateSubPaths(source))
        {
            if (dashPattern == null)
            {
                MapToPenSpace(subPath.Segments, toPenSpace);

                if (subPath.IsClosed)
                {
                    AddClosedOutline(result, subPath.Segments, halfWidth, offsetTolerance, style);
                }
                else
                {
                    AddOpenOutline(result, subPath.Segments, halfWidth, offsetTolerance, style);
                }

                continue;
            }

            // The pattern is split before the pen space is entered, over the arc length it is written in.
            foreach (List<IPathSegment> piece in SplitDashes(subPath, dashPattern, style.DashPhase))
            {
                MapToPenSpace(piece, toPenSpace);
                AddOpenOutline(result, piece, halfWidth, offsetTolerance, style);
            }
        }

        return result.Detach();
    }

    /// <summary>
    /// A rough size for the buffer the outline is built into: the outline runs either side of the path, and
    /// every curve on it splits into more the wider the pen is against the tolerance it is held to. A guess,
    /// so the buffer still grows when a path outruns it.
    /// </summary>
    private static int GetOutlineCapacity(PdfPath source, float halfWidth, float offsetTolerance)
    {
        float splitFactor = MathF.Sqrt(halfWidth / offsetTolerance);
        int pieceCount = Math.Min(1 << MaxSubdivisionDepth, Math.Max(1, (int)splitFactor));

        return source.Buffer.Length * OutlineCapacityFactor * pieceCount;
    }

    private sealed class SubPath
    {
        public List<IPathSegment> Segments { get; } = [];

        public bool IsClosed { get; set; }
    }

    private static List<SubPath> EnumerateSubPaths(PdfPath path)
    {
        List<SubPath> result = [];
        SubPath? current = null;
        Vector2 currentPoint = default;
        Vector2 subPathStart = default;

        foreach (PdfPathSegment segment in path.Segments)
        {
            switch (segment.Type)
            {
                case PdfPathSegmentType.MoveTo:
                {
                    if (current != null)
                    {
                        result.Add(current);
                    }

                    subPathStart = ToVector2(segment.Points[0]);
                    currentPoint = subPathStart;
                    current = new SubPath();
                    break;
                }
                case PdfPathSegmentType.LineTo:
                {
                    current ??= new SubPath();
                    Vector2 end = ToVector2(segment.Points[0]);
                    current.Segments.Add(new LinearPathSegment(currentPoint, end));
                    currentPoint = end;
                    break;
                }
                case PdfPathSegmentType.CubicTo:
                {
                    current ??= new SubPath();
                    Vector2 control1 = ToVector2(segment.Points[0]);
                    Vector2 control2 = ToVector2(segment.Points[1]);
                    Vector2 end = ToVector2(segment.Points[2]);
                    current.Segments.Add(new CubicPathSegment(currentPoint, control1, control2, end));
                    currentPoint = end;
                    break;
                }
                case PdfPathSegmentType.Close:
                {
                    if (current != null && current.Segments.Count > 0)
                    {
                        if (!PointsEqual(currentPoint, subPathStart))
                        {
                            current.Segments.Add(new LinearPathSegment(currentPoint, subPathStart));
                        }

                        current.IsClosed = true;
                        currentPoint = subPathStart;
                    }

                    break;
                }
            }
        }

        if (current != null)
        {
            result.Add(current);
        }

        return result;
    }

    /// <summary>
    /// <see cref="DeviceOffsetTolerance"/> expressed in the space the offsetting emits its points in, given
    /// the <paramref name="toDevice"/> matrix carrying that space to the device. The axis that magnifies
    /// most sets the tolerance, so the looser axis of a lopsided matrix cannot hide error on the tighter one.
    /// A matrix that maps everything onto a point, or off the scale a float can hold, leaves the tolerance
    /// as it stands.
    /// </summary>
    private static float GetOffsetTolerance(in PdfMatrix toDevice)
    {
        Vector2 xAxis = new(toDevice.ScaleX, toDevice.SkewY);
        Vector2 yAxis = new(toDevice.SkewX, toDevice.ScaleY);

        float magnification = MathF.Max(xAxis.Length(), yAxis.Length());
        if (magnification <= 0f || float.IsInfinity(magnification))
        {
            return DeviceOffsetTolerance;
        }

        return DeviceOffsetTolerance / magnification;
    }

    /// <summary>
    /// The dash pattern <paramref name="style"/> is to be dashed with, or null when it is to be stroked
    /// solid. A pattern whose entries add up to nothing never advances along the path and stands for a
    /// solid stroke.
    /// </summary>
    private static float[]? GetDashPattern(PdfStrokeStyle style)
    {
        float[]? pattern = style.DashPattern;
        if (pattern == null || pattern.Length == 0)
        {
            return null;
        }

        float total = 0f;
        foreach (float entry in pattern)
        {
            total += MathF.Max(entry, 0f);
        }

        return (total > Epsilon) ? pattern : null;
    }

    // A round pen is already in its own space and the segments are left as they are.
    private static void MapToPenSpace(List<IPathSegment> segments, in PdfMatrix toPenSpace)
    {
        if (toPenSpace.IsIdentity)
        {
            return;
        }

        for (int index = 0; index < segments.Count; index++)
        {
            segments[index] = segments[index].Transform(toPenSpace);
        }
    }
}
