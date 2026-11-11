using PdfPixel.Color;
using PdfPixel.Snap.Color;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Shading;

/// <summary>
/// An axial or radial shading's colors along t from 0 to 1, interpolated in the shading's color space and then
/// converted, one entry per device pixel t runs across.
/// </summary>
internal sealed class SnapGradientRamp
{
    public const int MaximumEntryCount = 1024;

    private const int MinimumEntryCount = 2;

    private readonly float[] _colors;
    private readonly int _componentCount;
    private readonly int _entryCount;

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public SnapGradientRamp(PdfColor[] stopColors, float[] stopPositions, float deviceLength, SnapColorConverter converter, int componentCount)
    {
        _componentCount = componentCount;
        _entryCount = (int)Math.Max(MinimumEntryCount, Math.Min(MaximumEntryCount, Math.Ceiling(deviceLength) + 1));
        _colors = new float[_entryCount * componentCount];

        var components = new float[stopColors[0].Components.Length];
        int stopIndex = 0;

        for (int entry = 0; entry < _entryCount; entry++)
        {
            float t = entry / (float)(_entryCount - 1);

            while (stopIndex < stopPositions.Length - 2 && t > stopPositions[stopIndex + 1])
            {
                stopIndex++;
            }

            int nextIndex = Math.Min(stopIndex + 1, stopPositions.Length - 1);
            float span = stopPositions[nextIndex] - stopPositions[stopIndex];
            float weight = (span > 0) ? Math.Max(0, Math.Min(1, (t - stopPositions[stopIndex]) / span)) : 0;
            ReadOnlySpan<float> start = stopColors[stopIndex].Components.Span;
            ReadOnlySpan<float> end = stopColors[nextIndex].Components.Span;

            for (int component = 0; component < components.Length; component++)
            {
                components[component] = start[component] + ((end[component] - start[component]) * weight);
            }

            converter.Convert(ref components[0], ref _colors[entry * componentCount]);
        }
    }

    /// <summary>
    /// Returns the color at <paramref name="t"/>, clamped to 0 to 1.
    /// </summary>
    public ReadOnlySpan<float> GetColor(float t)
    {
        float clamped = Math.Max(0, Math.Min(1, t));
        var entry = (int)((clamped * (_entryCount - 1)) + 0.5f);

        return new ReadOnlySpan<float>(_colors, entry * _componentCount, _componentCount);
    }
}
