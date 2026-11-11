using System;

namespace PdfPixel.Snap.Painting;

/// <summary>
/// Values of a run of pixels: one set per pixel, or one set for the whole run.
/// </summary>
internal readonly ref struct SnapRowValues
{
    private static readonly float[] _one = [1f];

    public SnapRowValues(scoped in ReadOnlySpan<float> values, bool isConstant)
    {
        Values = values;
        IsConstant = isConstant;
    }

    /// <summary>
    /// Gets the value 1 for every pixel of a run.
    /// </summary>
    public static SnapRowValues One => new(_one, true);

    public ReadOnlySpan<float> Values { get; }

    /// <summary>
    /// Gets whether every pixel has the first pixel's values.
    /// </summary>
    public bool IsConstant { get; }

    /// <summary>
    /// Gets whether every pixel has the value 1.
    /// </summary>
    public bool IsOne => IsConstant && Values[0] == 1f;

    /// <summary>
    /// Gets how far a reference into <see cref="Values"/> moves per pixel: 0 for constant values, 1 otherwise.
    /// </summary>
    public int Step => IsConstant ? 0 : 1;
}
