using PdfPixel.Color.Paint;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Builds the fill outline of a stroked <see cref="PdfPath"/> directly in <see cref="PdfPath"/> geometry
/// using <see cref="PdfPaint"/> parameters.
/// </summary>
public static partial class PdfStrokeOutlineBuilder
{
    // Maximum deviation of an emitted offset curve from the true offset, in device units, measured at
    // the segment midpoint.
    private const float DeviceOffsetTolerance = 0.1f;

    // A dash pattern that would cut a sub-path into more pieces than this leaves it solid instead.
    private const int MaxDashPieces = 10000;

    private const int OutlineCapacityFactor = 2;

    /// <summary>
    /// Builds the fill outline of <paramref name="source"/> stroked with the pen <paramref name="penMatrix"/>
    /// shapes: the circle of the line width in <paramref name="style"/> put through that matrix. The outline
    /// and the dash pattern are both in the space <paramref name="source"/> is given in, and the curve
    /// flattening tolerance is measured in the device space <paramref name="deviceMatrix"/> maps into.
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

        float offsetTolerance = GetOffsetTolerance(penMatrix.PostConcat(deviceMatrix));

        // The offsetting runs in pen space, where the pen is a circle; the builder carries points back.
        PdfMatrix toPenSpace = penMatrix.Invert();
        PdfPathBuilder result = new(GetOutlineCapacity(source, halfWidth, offsetTolerance), penMatrix);

        float[]? dashPattern = GetDashPattern(style);
        List<PdfContourMeasure> contours = PdfPathMeasure.GetContours(source);

        for (int contourIndex = 0; contourIndex < contours.Count; contourIndex++)
        {
            PdfContourMeasure contour = contours[contourIndex];

            if (dashPattern == null)
            {
                MapToPenSpace(contour.Segments, toPenSpace);

                if (contour.IsClosed)
                {
                    AddClosedOutline(result, contour.Segments, halfWidth, offsetTolerance, style);
                }
                else
                {
                    AddOpenOutline(result, contour.Segments, halfWidth, offsetTolerance, style);
                }

                continue;
            }

            // Split before entering pen space, over the arc length the pattern is written in.
            foreach (List<IPdfContourSegment> piece in SplitDashes(contour, dashPattern, style.DashPhase))
            {
                MapToPenSpace(piece, toPenSpace);
                AddOpenOutline(result, piece, halfWidth, offsetTolerance, style);
            }
        }

        return result.Detach();
    }

    /// <summary>
    /// Estimates the buffer size the outline is built into. The buffer still grows when a path outruns it.
    /// </summary>
    private static int GetOutlineCapacity(PdfPath source, float halfWidth, float offsetTolerance)
    {
        float splitFactor = MathF.Sqrt(halfWidth / offsetTolerance);
        int pieceCount = Math.Min(1 << PdfCubicContourSegment.MaxSubdivisionDepth, Math.Max(1, (int)splitFactor));

        return source.Buffer.Length * OutlineCapacityFactor * pieceCount;
    }

    /// <summary>
    /// <see cref="DeviceOffsetTolerance"/> expressed in the space <paramref name="toDevice"/> maps from,
    /// taking the magnification from the axis that magnifies most. A matrix that maps everything onto a
    /// point, or off the scale a float can hold, leaves the tolerance as it stands.
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
    /// The dash pattern <paramref name="style"/> is to be dashed with, or <see langword="null"/> when it is
    /// to be stroked solid. A pattern whose entries add up to nothing counts as solid.
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

        return (total > PdfVectorMath.Epsilon) ? pattern : null;
    }

    private static void MapToPenSpace(List<IPdfContourSegment> segments, in PdfMatrix toPenSpace)
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
