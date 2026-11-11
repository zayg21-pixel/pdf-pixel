using System;

namespace PdfPixel.Snap.Pattern;

/// <summary>
/// One axis of a tiling pattern's rasterized window: one period of whole pixels that coordinates wrap into when the
/// painted area spans a step, otherwise just the stretch the painted area reaches.
/// </summary>
internal readonly struct SnapTilingAxis
{
    private SnapTilingAxis(float origin, float scale, int size, bool wraps, float step)
    {
        Origin = origin;
        Scale = scale;
        Size = size;
        Wraps = wraps;
        Step = step;
    }

    /// <summary>
    /// Gets where the window starts, in pattern space.
    /// </summary>
    public float Origin { get; }

    /// <summary>
    /// Gets the window pixels per pattern unit.
    /// </summary>
    public float Scale { get; }

    public int Size { get; }

    public bool Wraps { get; }

    public float Step { get; }

    /// <summary>
    /// Returns the axis for a painted area of <paramref name="paintedLength"/> from <paramref name="paintedStart"/> in
    /// pattern space.
    /// </summary>
    public static SnapTilingAxis Create(float paintedStart, float paintedLength, float step, float deviceStep)
    {
        float pixelsPerUnit = deviceStep / step;

        if (paintedLength >= step)
        {
            int periodSize = Math.Max(1, (int)Math.Ceiling(deviceStep));

            return new SnapTilingAxis(0, periodSize / step, periodSize, wraps: true, step);
        }
        else
        {
            int windowSize = Math.Max(1, (int)Math.Ceiling(paintedLength * pixelsPerUnit));

            return new SnapTilingAxis(paintedStart, pixelsPerUnit, windowSize, wraps: false, step);
        }
    }

    /// <summary>
    /// Returns the window pixel a pattern coordinate falls in.
    /// </summary>
    public int GetIndex(float coordinate)
    {
        float offset = coordinate - Origin;

        if (Wraps)
        {
            offset = coordinate - ((float)Math.Floor(coordinate / Step) * Step);
        }

        return Math.Max(0, Math.Min(Size - 1, (int)(offset * Scale)));
    }
}
