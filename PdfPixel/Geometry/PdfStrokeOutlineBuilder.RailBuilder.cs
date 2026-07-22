using PdfPixel.Color.Paint;
using System.Collections.Generic;
using System.Numerics;

namespace PdfPixel.Geometry;

internal static partial class PdfStrokeOutlineBuilder
{
    private static void AddOpenOutline(PdfPathBuilder result, List<IPathSegment> segments, float halfWidth, PdfStrokeStyle style)
    {
        segments = RemoveDegenerate(segments);
        if (segments.Count == 0)
        {
            return;
        }

        RailBuilder forwardRail = new(result, halfWidth, style.LineJoin, style.MiterLimit);
        forwardRail.EmitChain(segments);

        IPathSegment lastForward = segments[segments.Count - 1];
        AddCap(result, lastForward.End, lastForward.EndTangent(), halfWidth, style.LineCap);

        RailBuilder backwardRail = new(result, halfWidth, style.LineJoin, style.MiterLimit);
        backwardRail.EmitChain(segments, reversed: true, continueFromCurrentPoint: true);

        IPathSegment lastBackward = segments[0].Reversed();
        AddCap(result, lastBackward.End, lastBackward.EndTangent(), halfWidth, style.LineCap);

        result.Close();
    }

    private static void AddClosedOutline(PdfPathBuilder result, List<IPathSegment> segments, float halfWidth, PdfStrokeStyle style)
    {
        segments = RemoveDegenerate(segments);
        if (segments.Count == 0)
        {
            return;
        }

        // Outer ring: offset to the left of travel, joined all the way around back to the start.
        RailBuilder outer = new(result, halfWidth, style.LineJoin, style.MiterLimit);
        outer.EmitChain(segments);
        outer.CloseJoinToStart();
        result.Close();

        // Inner ring: offset to the left of the reversed direction of travel (i.e. the other physical side),
        // joined all the way around. Produced as a second, separate closed contour; Winding fill turns the
        // two concentric rings into the annular stroke region.
        RailBuilder inner = new(result, halfWidth, style.LineJoin, style.MiterLimit);
        inner.EmitChain(segments, reversed: true);
        inner.CloseJoinToStart();
        result.Close();
    }

    /// <summary>
    /// Returns <paramref name="segments"/> with degenerate entries removed. Returns the original list
    /// unchanged (no allocation) when nothing needs removing, which is the common case.
    /// </summary>
    private static List<IPathSegment> RemoveDegenerate(List<IPathSegment> segments)
    {
        List<IPathSegment>? result = null;

        for (int i = 0; i < segments.Count; i++)
        {
            IPathSegment segment = segments[i];
            if (segment.IsDegenerate)
            {
                if (result == null)
                {
                    result = new List<IPathSegment>(segments.Count - 1);
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

    /// <summary>
    /// Emits one offset rail (this rail's own left side, relative to its own direction of travel) and the
    /// joins between its segments. Two independent instances, driven with the same segment chain read
    /// forward and in reverse respectively, together form the two sides of an open stroke; caps connect
    /// them at the ends.
    /// </summary>
    private struct RailBuilder
    {
        private readonly PdfPathBuilder _result;
        private readonly float _halfWidth;
        private readonly PdfStrokeJoin _join;
        private readonly float _miterLimit;
        private Vector2 _previousTangent;
        private Vector2 _firstTangent;
        private Vector2 _previousVertex;
        private Vector2 _firstVertex;
        private bool _hasPrevious;

        /// <summary>
        /// Builds one rail. A join whose sweep puts it on this rail's concave side is always replaced with a
        /// plain connecting line instead of the configured join shape: at a concave corner the offset
        /// segments already meet (or overlap, resolved by the winding fill) on their own, so outward join
        /// geometry there is redundant at best and, for miter in particular, can shoot a spike that
        /// self-intersects with reversed winding and punches a hole in the fill.
        /// </summary>
        public RailBuilder(PdfPathBuilder result, float halfWidth, PdfStrokeJoin join, float miterLimit)
        {
            _result = result;
            _halfWidth = halfWidth;
            _join = join;
            _miterLimit = miterLimit;
        }

        /// <summary>
        /// Emits this rail's segments. When <paramref name="reversed"/> is true, <paramref name="segments"/>
        /// is walked back to front with each segment flipped, instead of a separately materialized reversed
        /// list. When <paramref name="continueFromCurrentPoint"/> is true, the first segment neither moves to
        /// a new subpath nor joins — the builder's current point is assumed to already be that segment's
        /// offset start (e.g. left there by a cap bridging from another rail).
        /// </summary>
        public void EmitChain(List<IPathSegment> segments, bool reversed = false, bool continueFromCurrentPoint = false)
        {
            int count = segments.Count;
            for (int i = 0; i < count; i++)
            {
                IPathSegment segment = reversed ? segments[count - 1 - i].Reversed() : segments[i];
                Vector2 tangent = segment.StartTangent();

                if (!_hasPrevious)
                {
                    if (!continueFromCurrentPoint)
                    {
                        Vector2 offsetStart = Offset(segment.Start, tangent, _halfWidth);
                        _result.MoveTo(ToPdfPoint(offsetStart));
                    }

                    _firstVertex = segment.Start;
                    _firstTangent = tangent;
                }
                else
                {
                    AddJoin(_result, _previousVertex, _previousTangent, tangent, _halfWidth, _join, _miterLimit);
                }

                segment.EmitOffset(_result, _halfWidth);

                _previousVertex = segment.End;
                _previousTangent = segment.EndTangent();
                _hasPrevious = true;
            }
        }

        /// <summary>
        /// Inserts the join between the last emitted segment and the first, for a rail that wraps back
        /// around to its own starting vertex (a closed sub-path's ring).
        /// </summary>
        public void CloseJoinToStart()
        {
            if (!_hasPrevious)
            {
                return;
            }

            AddJoin(_result, _firstVertex, _previousTangent, _firstTangent, _halfWidth, _join, _miterLimit);
        }
    }
}
