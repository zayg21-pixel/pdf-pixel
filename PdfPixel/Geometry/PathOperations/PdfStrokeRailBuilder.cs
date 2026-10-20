using PdfPixel.Color.Paint;
using System.Collections.Generic;
using System.Numerics;

namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Emits one offset rail — this rail's own left side, relative to its own direction of travel — and the
/// joins between its segments. Two instances driven with the same segment chain, one forward and one
/// reversed, form the two sides of an open stroke; caps connect them at the ends.
/// </summary>
internal struct PdfStrokeRailBuilder
{
    private readonly PdfPathBuilder _result;
    private readonly float _halfWidth;
    private readonly float _offsetTolerance;
    private readonly PdfStrokeJoin _join;
    private readonly float _miterLimit;
    private Vector2 _previousTangent;
    private Vector2 _firstTangent;
    private Vector2 _previousVertex;
    private Vector2 _firstVertex;
    private bool _hasPrevious;

    /// <summary>
    /// Builds one rail. A join landing on this rail's concave side becomes a plain connecting line
    /// rather than the configured join shape — the offset segments already meet there on their own, and
    /// a miter in particular would shoot a spike that self-intersects with reversed winding and punches
    /// a hole in the fill.
    /// </summary>
    public PdfStrokeRailBuilder(PdfPathBuilder result, float halfWidth, float offsetTolerance, PdfStrokeJoin join, float miterLimit)
    {
        _result = result;
        _halfWidth = halfWidth;
        _offsetTolerance = offsetTolerance;
        _join = join;
        _miterLimit = miterLimit;
    }

    /// <summary>
    /// Emits this rail's segments, walking <paramref name="segments"/> back to front with each segment
    /// flipped when <paramref name="reversed"/> is set. With <paramref name="continueFromCurrentPoint"/>
    /// the first segment neither moves nor joins, because the builder's current point is already that
    /// segment's offset start — where a cap bridging from the other rail leaves it.
    /// </summary>
    public void EmitChain(List<IPdfContourSegment> segments, bool reversed = false, bool continueFromCurrentPoint = false)
    {
        int count = segments.Count;
        for (int i = 0; i < count; i++)
        {
            IPdfContourSegment segment = reversed ? segments[count - 1 - i] : segments[i];

            Vector2 startVertex = reversed ? segment.End : segment.Start;
            Vector2 endVertex = reversed ? segment.Start : segment.End;
            Vector2 startTangent = reversed ? -segment.EndTangent() : segment.StartTangent();
            Vector2 endTangent = reversed ? -segment.StartTangent() : segment.EndTangent();

            if (!_hasPrevious)
            {
                if (!continueFromCurrentPoint)
                {
                    Vector2 offsetStart = PdfVectorMath.Offset(startVertex, startTangent, _halfWidth);
                    _result.MoveTo(PdfVectorMath.ToPdfPoint(offsetStart));
                }

                _firstVertex = startVertex;
                _firstTangent = startTangent;
            }
            else
            {
                PdfStrokeOutlineBuilder.AddJoin(_result, _previousVertex, _previousTangent, startTangent, _halfWidth, _join, _miterLimit);
            }

            segment.EmitOffset(_result, _halfWidth, _offsetTolerance, startTangent, endTangent, reversed);

            _previousVertex = endVertex;
            _previousTangent = endTangent;
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

        PdfStrokeOutlineBuilder.AddJoin(_result, _firstVertex, _previousTangent, _firstTangent, _halfWidth, _join, _miterLimit);
    }
}
