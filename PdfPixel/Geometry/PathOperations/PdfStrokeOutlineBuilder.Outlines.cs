using PdfPixel.Color.Paint;
using System.Collections.Generic;

namespace PdfPixel.Geometry.PathOperations;

public static partial class PdfStrokeOutlineBuilder
{
    private static void AddOpenOutline(PdfPathBuilder result, List<IPdfContourSegment> segments, float halfWidth, float offsetTolerance, PdfStrokeStyle style)
    {
        segments = RemoveDegenerate(segments);
        if (segments.Count == 0)
        {
            return;
        }

        PdfStrokeRailBuilder forwardRail = new(result, halfWidth, offsetTolerance, style.LineJoin, style.MiterLimit);
        forwardRail.EmitChain(segments);

        IPdfContourSegment lastForward = segments[segments.Count - 1];
        AddCap(result, lastForward.End, lastForward.EndTangent(), halfWidth, style.LineCap);

        PdfStrokeRailBuilder backwardRail = new(result, halfWidth, offsetTolerance, style.LineJoin, style.MiterLimit);
        backwardRail.EmitChain(segments, reversed: true, continueFromCurrentPoint: true);

        IPdfContourSegment firstForward = segments[0];
        AddCap(result, firstForward.Start, -firstForward.StartTangent(), halfWidth, style.LineCap);

        result.Close();
    }

    private static void AddClosedOutline(PdfPathBuilder result, List<IPdfContourSegment> segments, float halfWidth, float offsetTolerance, PdfStrokeStyle style)
    {
        segments = RemoveDegenerate(segments);
        if (segments.Count == 0)
        {
            return;
        }

        // Outer ring: offset to the left of travel, joined all the way around back to the start.
        PdfStrokeRailBuilder outer = new(result, halfWidth, offsetTolerance, style.LineJoin, style.MiterLimit);
        outer.EmitChain(segments);
        outer.CloseJoinToStart();
        result.Close();

        // Inner ring: offset to the left of the reversed direction of travel (i.e. the other physical side),
        // joined all the way around. Produced as a second, separate closed contour; Winding fill turns the
        // two concentric rings into the annular stroke region.
        PdfStrokeRailBuilder inner = new(result, halfWidth, offsetTolerance, style.LineJoin, style.MiterLimit);
        inner.EmitChain(segments, reversed: true);
        inner.CloseJoinToStart();
        result.Close();
    }

    /// <summary>
    /// Returns <paramref name="segments"/> with degenerate entries removed, or the same list when there
    /// are none.
    /// </summary>
    private static List<IPdfContourSegment> RemoveDegenerate(List<IPdfContourSegment> segments)
    {
        List<IPdfContourSegment>? result = null;

        for (int i = 0; i < segments.Count; i++)
        {
            IPdfContourSegment segment = segments[i];
            if (segment.IsDegenerate)
            {
                if (result == null)
                {
                    result = new List<IPdfContourSegment>(segments.Count - 1);
                    for (int j = 0; j < i; j++)
                    {
                        result.Add(segments[j]);
                    }
                }

                continue;
            }

            result?.Add(segment);
        }

        return result ?? segments;
    }
}
