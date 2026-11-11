using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// Scan converts outlines into coverage rows, top to bottom, holding only the current row. Area coverage accumulates
/// the signed area each edge sweeps across the row (as FreeType's smooth rasterizer); point sampling covers the pixels
/// whose centers are inside, so abutting shapes share an edge with neither gap nor overlap.
/// </summary>
internal sealed class SnapRasterizer
{
    // Coverage within half an 8-bit output level of 0 or 1 is indistinguishable from it in the output, and counts as
    // that, so that the inside of an outline is a full run despite rounding in the accumulated area.
    private const float EmptyCoverage = 0.5f / 255;
    private const float FullCoverage = 1 - (0.5f / 255);

    private const int MinimumCapacity = 16;

    private PreparedEdge[] _edges = [];
    private float[] _edgeTops = [];
    private int[] _activeEdges = [];
    private float[] _accumulation = [];
    private float[] _coverage = [];
    private SnapCoverageRun[] _runs = [];
    private float[] _crossingPositions = [];
    private int[] _crossingWindings = [];
    private int _touchedLeft;
    private int _touchedRight;

    /// <summary>
    /// Writes the coverage of <paramref name="edges"/> within <paramref name="limits"/> to <paramref name="sink"/>.
    /// </summary>
    public void Rasterize<TSink>(SnapEdgeList edges, PdfPathFillType fillType, bool areaCoverage, in PdfIntegerRectangle limits, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        ReadOnlySpan<SnapEdge> edgeSpan = edges.Edges;

        if (edgeSpan.Length == 0)
        {
            return;
        }

        PdfIntegerRectangle edgeBounds = PdfSnapGrid.RoundOut(new PdfRectangle(edges.MinX, edges.MinY, edges.MaxX, edges.MaxY));
        PdfIntegerRectangle bounds = PdfIntegerRectangle.Intersect(edgeBounds, limits);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        PrepareEdges(edgeSpan, bounds.Left);
        PrepareRow(bounds.Width);

        if (areaCoverage)
        {
            RasterizeArea(edgeSpan.Length, fillType, bounds, ref sink);
        }
        else
        {
            RasterizePoints(edgeSpan.Length, fillType, bounds, ref sink);
        }
    }

    /// <summary>
    /// Writes one full run per row of the whole-pixel <paramref name="rectangle"/> within <paramref name="limits"/>.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public void FillRectangle<TSink>(in PdfIntegerRectangle rectangle, in PdfIntegerRectangle limits, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        PdfIntegerRectangle bounds = PdfIntegerRectangle.Intersect(rectangle, limits);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        EnsureCapacity(ref _runs, 1);
        _runs[0] = new SnapCoverageRun(bounds.Left, bounds.Width, isFull: true);
        ReadOnlySpan<SnapCoverageRun> runs = _runs.AsSpan(0, 1);

        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            SnapCoverageRow row = new(y, bounds.Left, runs, ReadOnlySpan<float>.Empty);
            sink.AddRow(row);
        }
    }

    private static void EnsureCapacity<T>(ref T[] array, int count)
    {
        if (array.Length < count)
        {
            array = new T[Math.Max(Math.Max(MinimumCapacity, count), array.Length * 2)];
        }
    }

    private static float ApplyFillRule(float winding, PdfPathFillType fillType)
    {
        float magnitude = Math.Abs(winding);

        if (fillType == PdfPathFillType.EvenOdd)
        {
            magnitude -= 2 * (float)Math.Floor(magnitude * 0.5f);

            return (magnitude > 1) ? 2 - magnitude : magnitude;
        }
        else
        {
            return Math.Min(magnitude, 1f);
        }
    }

    private static void AddSplit(in Span<float> splits, ref int splitCount, float t)
    {
        if (t > 0 && t < 1)
        {
            splits[splitCount] = t;
            splitCount++;
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void PrepareEdges(in ReadOnlySpan<SnapEdge> edges, int left)
    {
        int edgeCount = edges.Length;

        EnsureCapacity(ref _edges, edgeCount);
        EnsureCapacity(ref _edgeTops, edgeCount);
        EnsureCapacity(ref _activeEdges, edgeCount);
        EnsureCapacity(ref _crossingPositions, edgeCount);
        EnsureCapacity(ref _crossingWindings, edgeCount);

        for (int index = 0; index < edgeCount; index++)
        {
            PreparedEdge prepared = new(edges[index], left);
            _edges[index] = prepared;
            _edgeTops[index] = prepared.Top;
        }

        Array.Sort(_edgeTops, _edges, 0, edgeCount);
    }

    private void PrepareRow(int width)
    {
        EnsureCapacity(ref _coverage, width);
        EnsureCapacity(ref _accumulation, width + 2);
        EnsureCapacity(ref _runs, width);
        Array.Clear(_accumulation, 0, width + 2);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void RasterizeArea<TSink>(int edgeCount, PdfPathFillType fillType, in PdfIntegerRectangle bounds, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        int width = bounds.Width;
        int nextEdge = 0;
        int activeCount = 0;

        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            float rowBottom = y + 1;

            while (nextEdge < edgeCount && _edgeTops[nextEdge] < rowBottom)
            {
                _activeEdges[activeCount] = nextEdge;
                activeCount++;
                nextEdge++;
            }

            if (activeCount == 0)
            {
                continue;
            }

            _touchedLeft = int.MaxValue;
            _touchedRight = int.MinValue;
            int keptCount = 0;

            for (int activeIndex = 0; activeIndex < activeCount; activeIndex++)
            {
                int edgeIndex = _activeEdges[activeIndex];
                ref PreparedEdge edge = ref _edges[edgeIndex];

                if (edge.Bottom <= y)
                {
                    continue;
                }

                _activeEdges[keptCount] = edgeIndex;
                keptCount++;

                float top = Math.Max(edge.Top, y);
                float bottom = Math.Min(edge.Bottom, rowBottom);
                AccumulateClippedPiece(edge.GetX(top), edge.GetX(bottom), (bottom - top) * edge.Direction, width);
            }

            activeCount = keptCount;

            if (_touchedLeft <= _touchedRight)
            {
                EmitAreaRow(y, bounds.Left, width, fillType, ref sink);
            }
        }
    }

    /// <summary>
    /// Adds a piece of an edge with its parts outside columns 0 to <paramref name="width"/> moved onto the nearest
    /// boundary, where they affect the columns inside the same way.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulateClippedPiece(float startX, float endX, float signedHeight, int width)
    {
        if (startX >= 0 && startX <= width && endX >= 0 && endX <= width)
        {
            AccumulatePiece(startX, endX, signedHeight);
            return;
        }

        Span<float> splits = stackalloc float[4];
        int splitCount = 0;
        splits[splitCount] = 0;
        splitCount++;

        float deltaX = endX - startX;

        if (deltaX != 0)
        {
            AddSplit(splits, ref splitCount, (0 - startX) / deltaX);
            AddSplit(splits, ref splitCount, (width - startX) / deltaX);
        }

        splits[splitCount] = 1;
        splitCount++;

        if (splitCount == 4 && splits[1] > splits[2])
        {
            (splits[1], splits[2]) = (splits[2], splits[1]);
        }

        for (int index = 1; index < splitCount; index++)
        {
            float pieceStart = splits[index - 1];
            float pieceEnd = splits[index];
            float pieceStartX = Math.Max(0, Math.Min(width, startX + (deltaX * pieceStart)));
            float pieceEndX = Math.Max(0, Math.Min(width, startX + (deltaX * pieceEnd)));

            AccumulatePiece(pieceStartX, pieceEndX, signedHeight * (pieceEnd - pieceStart));
        }
    }

    /// <summary>
    /// Adds the signed area a piece of an edge sweeps within the row to the accumulation, whose running sum along the
    /// row is the winding number integrated over each pixel.
    /// </summary>
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void AccumulatePiece(float startX, float endX, float signedHeight)
    {
        float left = Math.Min(startX, endX);
        float right = Math.Max(startX, endX);
        var leftFloor = (float)Math.Floor(left);
        var leftColumn = (int)leftFloor;
        var rightCeiling = (float)Math.Ceiling(right);
        var rightColumn = (int)rightCeiling;
        ref float leftCell = ref Unsafe.Add(ref MemoryMarshal.GetReference(_accumulation.AsSpan()), leftColumn);

        _touchedLeft = Math.Min(_touchedLeft, leftColumn);

        if (rightColumn <= leftColumn + 1)
        {
            float middle = (0.5f * (startX + endX)) - leftFloor;
            leftCell += signedHeight - (signedHeight * middle);
            Unsafe.Add(ref leftCell, 1) += signedHeight * middle;
            _touchedRight = Math.Max(_touchedRight, leftColumn + 1);
        }
        else
        {
            float inverseWidth = 1 / (right - left);
            float leftFraction = left - leftFloor;
            float firstArea = 0.5f * inverseWidth * (1 - leftFraction) * (1 - leftFraction);
            float rightFraction = right - rightCeiling + 1;
            float lastArea = 0.5f * inverseWidth * rightFraction * rightFraction;
            int columnCount = rightColumn - leftColumn;

            leftCell += signedHeight * firstArea;

            if (columnCount == 2)
            {
                Unsafe.Add(ref leftCell, 1) += signedHeight * (1 - firstArea - lastArea);
            }
            else
            {
                float secondArea = inverseWidth * (1.5f - leftFraction);
                float middleArea = signedHeight * inverseWidth;
                Unsafe.Add(ref leftCell, 1) += signedHeight * (secondArea - firstArea);
                ref float cell = ref Unsafe.Add(ref leftCell, 2);

                for (int column = 2; column < columnCount - 1; column++)
                {
                    cell += middleArea;
                    cell = ref Unsafe.Add(ref cell, 1);
                }

                float beforeLastArea = secondArea + ((columnCount - 3) * inverseWidth);
                Unsafe.Add(ref leftCell, columnCount - 1) += signedHeight * (1 - beforeLastArea - lastArea);
            }

            Unsafe.Add(ref leftCell, columnCount) += signedHeight * lastArea;
            _touchedRight = Math.Max(_touchedRight, rightColumn);
        }
    }

    /// <summary>
    /// Turns the accumulation between the touched columns into coverage runs and clears it; outside those columns the
    /// winding of closed contours is 0.
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void EmitAreaRow<TSink>(int y, int left, int width, PdfPathFillType fillType, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        int firstColumn = _touchedLeft;
        int endColumn = Math.Min(width, _touchedRight + 1);
        int runCount = 0;
        int runStart = 0;
        var runIsFull = false;
        var inRun = false;
        float winding = 0;
        ref float accumulation = ref Unsafe.Add(ref MemoryMarshal.GetReference(_accumulation.AsSpan()), firstColumn);
        ref float coverageValue = ref Unsafe.Add(ref MemoryMarshal.GetReference(_coverage.AsSpan()), firstColumn);

        for (int column = firstColumn; column < endColumn; column++)
        {
            winding += accumulation;
            float coverage = ApplyFillRule(winding, fillType);
            coverageValue = coverage;
            accumulation = ref Unsafe.Add(ref accumulation, 1);
            coverageValue = ref Unsafe.Add(ref coverageValue, 1);

            bool isCovered = coverage > EmptyCoverage;
            bool isFull = coverage >= FullCoverage;

            if (inRun && (!isCovered || isFull != runIsFull))
            {
                _runs[runCount] = new SnapCoverageRun(left + runStart, column - runStart, runIsFull);
                runCount++;
                inRun = false;
            }

            if (isCovered && !inRun)
            {
                runStart = column;
                runIsFull = isFull;
                inRun = true;
            }
        }

        if (inRun)
        {
            _runs[runCount] = new SnapCoverageRun(left + runStart, endColumn - runStart, runIsFull);
            runCount++;
        }

        Array.Clear(_accumulation, firstColumn, _touchedRight - firstColumn + 1);

        if (runCount > 0)
        {
            SnapCoverageRow row = new(y, left, _runs.AsSpan(0, runCount), _coverage.AsSpan(0, width));
            sink.AddRow(row);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void RasterizePoints<TSink>(int edgeCount, PdfPathFillType fillType, in PdfIntegerRectangle bounds, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        int nextEdge = 0;
        int activeCount = 0;

        for (int y = bounds.Top; y < bounds.Bottom; y++)
        {
            float centerY = y + 0.5f;

            // An edge holds the pixel centers in [top, bottom): a center on its lower end belongs to the edge below,
            // so a shared vertex is counted once.
            while (nextEdge < edgeCount && _edgeTops[nextEdge] <= centerY)
            {
                _activeEdges[activeCount] = nextEdge;
                activeCount++;
                nextEdge++;
            }

            int keptCount = 0;
            int crossingCount = 0;

            for (int activeIndex = 0; activeIndex < activeCount; activeIndex++)
            {
                int edgeIndex = _activeEdges[activeIndex];
                ref PreparedEdge edge = ref _edges[edgeIndex];

                if (centerY >= edge.Bottom)
                {
                    continue;
                }

                _activeEdges[keptCount] = edgeIndex;
                keptCount++;
                _crossingPositions[crossingCount] = edge.GetX(centerY);
                _crossingWindings[crossingCount] = (int)edge.Direction;
                crossingCount++;
            }

            activeCount = keptCount;

            if (crossingCount > 1)
            {
                Array.Sort(_crossingPositions, _crossingWindings, 0, crossingCount);
                EmitPointRow(y, bounds.Left, bounds.Width, crossingCount, fillType, ref sink);
            }
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void EmitPointRow<TSink>(int y, int left, int width, int crossingCount, PdfPathFillType fillType, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        int runCount = 0;
        int winding = 0;

        for (int index = 0; index < crossingCount - 1; index++)
        {
            winding += _crossingWindings[index];

            bool isInside = (fillType == PdfPathFillType.EvenOdd)
                ? (winding & 1) != 0
                : winding != 0;

            if (!isInside)
            {
                continue;
            }

            // Pixel i is covered when its center i + 0.5 lies in [start, end).
            var firstColumn = (int)Math.Max(0, Math.Ceiling(_crossingPositions[index] - 0.5));
            var endColumn = (int)Math.Min(width, Math.Ceiling(_crossingPositions[index + 1] - 0.5));

            if (endColumn <= firstColumn)
            {
                continue;
            }

            if (runCount > 0 && _runs[runCount - 1].Left + _runs[runCount - 1].Count == left + firstColumn)
            {
                SnapCoverageRun previous = _runs[runCount - 1];
                _runs[runCount - 1] = new SnapCoverageRun(previous.Left, previous.Count + endColumn - firstColumn, isFull: true);
            }
            else
            {
                _runs[runCount] = new SnapCoverageRun(left + firstColumn, endColumn - firstColumn, isFull: true);
                runCount++;
            }
        }

        if (runCount > 0)
        {
            SnapCoverageRow row = new(y, left, _runs.AsSpan(0, runCount), ReadOnlySpan<float>.Empty);
            sink.AddRow(row);
        }
    }

    /// <summary>
    /// An edge oriented top to bottom, its columns counted from the left of the rasterized area.
    /// </summary>
    private readonly struct PreparedEdge
    {
        private readonly float _topX;
        private readonly float _slope;
        private readonly float _minimumX;
        private readonly float _maximumX;

        public PreparedEdge(in SnapEdge edge, int left)
        {
            bool isDownward = edge.EndY > edge.StartY;
            float topX = (isDownward ? edge.StartX : edge.EndX) - left;
            float bottomX = (isDownward ? edge.EndX : edge.StartX) - left;

            Top = Math.Min(edge.StartY, edge.EndY);
            Bottom = Math.Max(edge.StartY, edge.EndY);
            Direction = isDownward ? 1 : -1;
            _topX = topX;
            _slope = (bottomX - topX) / (Bottom - Top);
            _minimumX = Math.Min(topX, bottomX);
            _maximumX = Math.Max(topX, bottomX);
        }

        public float Top { get; }

        public float Bottom { get; }

        /// <summary>
        /// Gets the winding the edge adds: 1 drawn downward, −1 drawn upward.
        /// </summary>
        public float Direction { get; }

        /// <summary>
        /// Returns where the edge crosses height <paramref name="y"/>, clamped between its ends.
        /// </summary>
        public float GetX(float y) => Math.Max(_minimumX, Math.Min(_maximumX, _topX + ((y - Top) * _slope)));
    }
}
