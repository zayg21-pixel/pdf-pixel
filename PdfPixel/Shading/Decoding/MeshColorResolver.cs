using PdfPixel.Color;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Models;
using PdfPixel.Shading.Model;
using System;
using System.Numerics;

namespace PdfPixel.Shading.Decoding;

/// <summary>
/// Resolves the colors of a mesh shading's vertices (types 4 to 7).
/// </summary>
/// <remarks>
/// When the shading dictionary carries a <c>Function</c> entry, a vertex stores a single parametric
/// value instead of color components. Such a value is interpolated across the mesh and the function
/// is evaluated for every interpolated value, so the resolver pre-samples the function over the
/// parametric range and interpolates between those samples.
/// </remarks>
internal sealed class MeshColorResolver
{
    private readonly ColorTransformSampler _sampler;
    private readonly Vector4[] _parametricColors;
    private readonly float _parameterMin;
    private readonly float _parameterScale;

    /// <summary>
    /// Initializes a resolver for the given shading.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">RGBA sampler for color conversion.</param>
    /// <param name="functionSamples">Number of samples taken from the shading's function(s).</param>
    public MeshColorResolver(PdfShading shading, ColorTransformSampler sampler, int functionSamples)
    {
        _sampler = sampler;

        if (shading.Functions.Count == 0)
        {
            _parametricColors = Array.Empty<Vector4>();
            return;
        }

        // The parametric value's range comes from the color component pair of the Decode array.
        PdfRange parameterRange = (shading.Decode?.Length >= 3) ? shading.Decode[2] : new PdfRange(0f, 1f);
        int sampleCount = Math.Max(2, functionSamples);
        _parametricColors = new Vector4[sampleCount];

        for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            float parameter = parameterRange.Denormalize(sampleIndex / (float)(sampleCount - 1));
            ReadOnlySpan<float> components = PdfFunctions.EvaluateColorFunctions(shading.Functions, parameter);
            _parametricColors[sampleIndex] = sampler.Sample(components);
        }

        _parameterMin = parameterRange.Min;
        _parameterScale = parameterRange.InverseRange * (sampleCount - 1);
    }

    /// <summary>
    /// True when vertices carry a single parametric value that has to be interpolated before the
    /// shading's function is evaluated; false when vertices carry color components directly.
    /// </summary>
    public bool IsParametric => _parametricColors.Length > 0;

    /// <summary>
    /// Builds the color data of a vertex from the components decoded for it.
    /// </summary>
    /// <param name="components">Decoded components: the parametric value alone when the shading
    /// carries a function, otherwise color components in the shading's color space.</param>
    public MeshVertexColor CreateVertexColor(in ReadOnlySpan<float> components)
    {
        if (IsParametric)
        {
            float parameter = components[0];
            return new MeshVertexColor(Resolve(parameter), parameter);
        }

        return new MeshVertexColor(_sampler.Sample(components).ToPdfColor(), 0f);
    }

    /// <summary>
    /// Resolves the color of a vertex interpolated across the mesh: the color the shading's
    /// function(s) produce for its parametric value, or its interpolated color when the shading
    /// carries no function.
    /// </summary>
    public PdfColor Resolve(in MeshVertexColor interpolated)
        => IsParametric ? Resolve(interpolated.Parameter) : interpolated.Color;

    private PdfColor Resolve(float parameter)
    {
        int lastIndex = _parametricColors.Length - 1;
        float position = (parameter - _parameterMin) * _parameterScale;

        if (position <= 0f)
        {
            return _parametricColors[0].ToPdfColor();
        }

        if (position >= lastIndex)
        {
            return _parametricColors[lastIndex].ToPdfColor();
        }

        var lowerIndex = (int)position;

        return Vector4.Lerp(_parametricColors[lowerIndex], _parametricColors[lowerIndex + 1], position - lowerIndex).ToPdfColor();
    }
}
