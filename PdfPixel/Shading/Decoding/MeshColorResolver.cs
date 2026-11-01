using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Functions;
using PdfPixel.Objects;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Shading.Decoding;

/// <summary>
/// Resolves the color components of a mesh shading's vertices (types 4 to 7).
/// </summary>
internal sealed class MeshColorResolver
{
    private readonly PdfPalette? _palette;
    private readonly float[] _parametricComponents;
    private readonly int _parametricSampleCount;
    private readonly float _parameterMin;
    private readonly float _parameterScale;

    /// <summary>
    /// Initializes a resolver for the given shading.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="palette">Palette the shading's color values index into, or null when its color space is not Indexed.</param>
    /// <param name="componentCount">Number of color components of every resolved vertex.</param>
    /// <param name="functionSamples">Number of samples taken from the shading's function(s).</param>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    public MeshColorResolver(PdfShading shading, PdfPalette? palette, int componentCount, int functionSamples)
    {
        _palette = palette;
        ComponentCount = componentCount;

        if (shading.Functions.Length == 0)
        {
            _parametricComponents = Array.Empty<float>();
            return;
        }

        // The parametric value's range comes from the color component pair of the Decode array.
        PdfRange parameterRange = (shading.Decode?.Length >= 3) ? shading.Decode[2] : new PdfRange(0f, 1f);
        _parametricSampleCount = Math.Max(2, functionSamples);
        _parametricComponents = new float[_parametricSampleCount * componentCount];

        for (int sampleIndex = 0; sampleIndex < _parametricSampleCount; sampleIndex++)
        {
            float parameter = parameterRange.Denormalize(sampleIndex / (float)(_parametricSampleCount - 1));
            ReadOnlySpan<float> components = PdfFunctions.EvaluateColorFunctions(shading.Functions, parameter);
            ResolveComponents(components, _parametricComponents.AsSpan(sampleIndex * componentCount, componentCount));
        }

        _parameterMin = parameterRange.Min;
        _parameterScale = parameterRange.InverseRange * (_parametricSampleCount - 1);
    }

    /// <summary>
    /// True when vertices carry a single parametric value that has to be interpolated before the
    /// shading's function is evaluated; false when vertices carry color components directly.
    /// </summary>
    public bool IsParametric => _parametricComponents.Length > 0;

    /// <summary>
    /// Gets the number of color components of every resolved vertex.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Builds the values of a vertex from the components decoded for it.
    /// </summary>
    /// <param name="components">Decoded components: the parametric value alone when the shading
    /// carries a function, otherwise color components in the shading's color space.</param>
    public PdfColor CreateVertexColor(float[] components)
    {
        if (IsParametric)
        {
            return new PdfColor(components);
        }

        if (_palette != null)
        {
            return _palette.GetColor((components.Length > 0) ? components[0] : 0f);
        }

        if (components.Length == ComponentCount)
        {
            return new PdfColor(components);
        }

        var vertexComponents = new float[ComponentCount];
        CopyComponents(components, vertexComponents);

        return new PdfColor(vertexComponents);
    }

    /// <summary>
    /// Writes the color components the shading's function(s) produce for <paramref name="parameter"/> to
    /// <paramref name="destination"/>.
    /// </summary>
    public void ResolveParameter(float parameter, in Span<float> destination)
    {
        int lastIndex = _parametricSampleCount - 1;
        float position = (parameter - _parameterMin) * _parameterScale;

        if (position <= 0f)
        {
            GetParametricSample(0).CopyTo(destination);
        }
        else if (position >= lastIndex)
        {
            GetParametricSample(lastIndex).CopyTo(destination);
        }
        else
        {
            var lowerIndex = (int)position;
            float fraction = position - lowerIndex;
            ReadOnlySpan<float> lowerSample = GetParametricSample(lowerIndex);
            ReadOnlySpan<float> upperSample = GetParametricSample(lowerIndex + 1);

            for (int componentIndex = 0; componentIndex < ComponentCount; componentIndex++)
            {
                destination[componentIndex] = lowerSample[componentIndex] + ((upperSample[componentIndex] - lowerSample[componentIndex]) * fraction);
            }
        }
    }

    private ReadOnlySpan<float> GetParametricSample(int sampleIndex)
        => _parametricComponents.AsSpan(sampleIndex * ComponentCount, ComponentCount);

    private void ResolveComponents(in ReadOnlySpan<float> source, in Span<float> destination)
    {
        if (_palette != null)
        {
            ReadOnlySpan<float> paletteComponents = _palette.GetColor((source.Length > 0) ? source[0] : 0f).Components.Span;
            CopyComponents(paletteComponents, destination);
        }
        else
        {
            CopyComponents(source, destination);
        }
    }

    private static void CopyComponents(in ReadOnlySpan<float> source, in Span<float> destination)
    {
        int count = Math.Min(source.Length, destination.Length);
        source.Slice(0, count).CopyTo(destination);
    }
}
