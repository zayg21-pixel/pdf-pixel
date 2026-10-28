using PdfPixel.Geometry;
using PdfPixel.Objects;
using System;

namespace PdfPixel.Annotations.Parsing;

/// <summary>
/// Parses annotation geometry: stroke lists (InkList), quadrilaterals (QuadPoints) and path operator arrays (Path).
/// </summary>
internal static class PdfAnnotationGeometryParser
{
    /// <summary>
    /// Parses an array of point arrays (InkList entry) into one point list per stroke.
    /// </summary>
    /// <param name="inkList">The ink list array, or null.</param>
    /// <returns>The parsed strokes, or an empty array if <paramref name="inkList"/> is null.</returns>
    public static PdfPoint[][] ParseInkList(PdfArray? inkList)
    {
        if (inkList == null)
        {
            return Array.Empty<PdfPoint[]>();
        }

        var strokes = new PdfPoint[inkList.Count][];

        for (int index = 0; index < inkList.Count; index++)
        {
            strokes[index] = PdfPoint.ParsePoints(inkList.GetArray(index));
        }

        return strokes;
    }

    /// <summary>
    /// Parses a QuadPoints array into quadrilaterals of four points each.
    /// </summary>
    /// <param name="quadPointsArray">The QuadPoints array, or null.</param>
    /// <returns>The parsed quadrilaterals, or an empty array if the array is null or its length is not a multiple of 8.</returns>
    public static PdfPoint[][] ParseQuadrilaterals(PdfArray? quadPointsArray)
    {
        if (quadPointsArray == null)
        {
            return Array.Empty<PdfPoint[]>();
        }

        float[] quadPoints = quadPointsArray.GetFloatArray();

        if (quadPoints.Length < 8 || quadPoints.Length % 8 != 0)
        {
            return Array.Empty<PdfPoint[]>();
        }

        int quadCount = quadPoints.Length / 8;
        var quads = new PdfPoint[quadCount][];

        for (int index = 0; index < quadCount; index++)
        {
            int offset = index * 8;

            // Deliberate deviation from the spec. ISO 32000-2 (Tables 176 and 182) lists each quadrilateral's
            // corners counterclockwise, starting with the bottom edge. Acrobat, and with it nearly every PDF
            // writer, stores them as top-left, top-right, bottom-left, bottom-right instead. Following the spec
            // would put underlines and strikeouts on the wrong edge in almost every real file, so the points are
            // read in the order writers actually use: the first two points below form the bottom edge.
            quads[index] =
            [
                new PdfPoint(quadPoints[offset + 6], quadPoints[offset + 7]),
                new PdfPoint(quadPoints[offset + 4], quadPoints[offset + 5]),
                new PdfPoint(quadPoints[offset + 0], quadPoints[offset + 1]),
                new PdfPoint(quadPoints[offset + 2], quadPoints[offset + 3])
            ];
        }

        return quads;
    }

    /// <summary>
    /// Parses an RD array of four insets into a rectangle holding the left, top, right and bottom differences, in that order.
    /// </summary>
    /// <param name="insetsArray">The RD array, or null.</param>
    /// <returns>The differences, or null if the array is null or has fewer than four numbers.</returns>
    public static PdfRectangle? ParseInsets(PdfArray? insetsArray)
    {
        if (insetsArray == null || insetsArray.Count < 4)
        {
            return null;
        }

        return new PdfRectangle(
            insetsArray.GetFloatOrDefault(0),
            insetsArray.GetFloatOrDefault(1),
            insetsArray.GetFloatOrDefault(2),
            insetsArray.GetFloatOrDefault(3));
    }

    /// <summary>
    /// Parses a Path array of path operator operands into a path: a moveto first, then a lineto
    /// for each 2-number entry and a curveto for each 6-number entry.
    /// </summary>
    /// <param name="pathArray">The Path array, or null.</param>
    /// <returns>The parsed path, or null if the array is null, empty, or contains an entry of invalid length.</returns>
    public static PdfPath? ParsePath(PdfArray? pathArray)
    {
        if (pathArray == null || pathArray.Count == 0)
        {
            return null;
        }

        PdfArray? moveToArray = pathArray.GetArray(0);

        if (moveToArray == null || moveToArray.Count != 2)
        {
            return null;
        }

        PdfPathBuilder builder = new();
        builder.MoveTo(moveToArray.GetFloatOrDefault(0), moveToArray.GetFloatOrDefault(1));

        for (int index = 1; index < pathArray.Count; index++)
        {
            PdfArray? operandArray = pathArray.GetArray(index);

            if (operandArray?.Count == 2)
            {
                builder.LineTo(operandArray.GetFloatOrDefault(0), operandArray.GetFloatOrDefault(1));
            }
            else if (operandArray?.Count == 6)
            {
                builder.CubicTo(
                    operandArray.GetFloatOrDefault(0),
                    operandArray.GetFloatOrDefault(1),
                    operandArray.GetFloatOrDefault(2),
                    operandArray.GetFloatOrDefault(3),
                    operandArray.GetFloatOrDefault(4),
                    operandArray.GetFloatOrDefault(5));
            }
            else
            {
                return null;
            }
        }

        return builder.ToPath();
    }
}
