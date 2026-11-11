using PdfPixel.Snap.Rasterization;
using System;

namespace PdfPixel.Snap.Clipping;

/// <summary>
/// The reusable row a clip merge writes into, grown to the widest row asked of it.
/// </summary>
internal sealed class SnapClipBuffer
{
    private const int MinimumCapacity = 16;

    private SnapCoverageRun[] _runs = [];
    private float[] _values = [];

    public SnapCoverageRun[] Runs => _runs;

    public float[] Values => _values;

    /// <summary>
    /// Grows the buffer to hold a row of <paramref name="width"/> pixels.
    /// </summary>
    public void Prepare(int width)
    {
        if (_runs.Length < width)
        {
            int capacity = Math.Max(Math.Max(MinimumCapacity, width), _runs.Length * 2);
            _runs = new SnapCoverageRun[capacity];
            _values = new float[capacity];
        }
    }
}
