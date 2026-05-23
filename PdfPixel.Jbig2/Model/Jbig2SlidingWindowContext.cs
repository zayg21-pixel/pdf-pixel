using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Carries per-group sliding window registers across rows during generic region decoding.
/// Created once before the row loop and reused for each row.
/// The hot loop in <see cref="Jbig2RowTemplate"/> accesses fields directly to avoid method call overhead.
/// </summary>
internal ref struct Jbig2SlidingWindowContext
{
    /// <summary>Per-group window registers. Index matches <see cref="Jbig2RowTemplate.SlidingGroups"/>.</summary>
    internal readonly Span<uint> Windows;

    /// <summary>Per-group row base offsets (byte offset into bitmap data for the source row).</summary>
    internal readonly Span<int> RowBases;

    /// <summary>Sliding group descriptors from the template.</summary>
    internal readonly Jbig2SlidingWindowGroup[] SlidingGroups;

    /// <summary>
    /// Creates a new sliding window context. Call once before the row loop.
    /// </summary>
    /// <param name="slidingGroups">Pre-computed sliding group descriptors.</param>
    /// <param name="windows">Caller-owned buffer for window registers (length = group count).</param>
    /// <param name="rowBases">Caller-owned buffer for row base offsets (length = group count).</param>
    /// <param name="width">Bitmap width in pixels.</param>
    /// <param name="height">Bitmap height in pixels.</param>
    /// <param name="stride">Bitmap stride in bytes.</param>
    internal Jbig2SlidingWindowContext(
        Jbig2SlidingWindowGroup[] slidingGroups,
        Span<uint> windows,
        Span<int> rowBases,
        int width,
        int height,
        int stride)
    {
        SlidingGroups = slidingGroups;
        Windows = windows;
        RowBases = rowBases;
    }
}
