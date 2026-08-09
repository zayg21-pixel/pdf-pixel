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
    // How far the emitted offset curve may stray from the true offset, measured at the segment midpoint,
    // before the cubic is split and its halves offset separately.
    private const float StrokeOffsetTolerance = 0.1f;
    private const int MaxSubdivisionDepth = 24;
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
    /// Builds the fill outline of <paramref name="source"/> stroked with <paramref name="style"/>.
    /// </summary>
    public static PdfPath BuildOutline(PdfPath source, PdfStrokeStyle style)
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

        PdfPathBuilder result = new(source.Buffer.Length * OutlineCapacityFactor);

        foreach (SubPath subPath in EnumerateSubPaths(source))
        {
            bool isDashed = style.DashPattern is { Length: > 0 };

            if (!isDashed)
            {
                if (subPath.IsClosed)
                {
                    AddClosedOutline(result, subPath.Segments, halfWidth, style);
                }
                else
                {
                    AddOpenOutline(result, subPath.Segments, halfWidth, style);
                }

                continue;
            }

            foreach (List<IPathSegment> piece in SplitDashes(subPath, style.DashPattern!, style.DashPhase))
            {
                AddOpenOutline(result, piece, halfWidth, style);
            }
        }

        return result.Detach();
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
}
