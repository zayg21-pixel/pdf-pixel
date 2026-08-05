using PdfPixel.Models;
using PdfPixel.Shading;
using PdfPixel.Shading.Model;
using System;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// Holds the built rendering primitives for one <see cref="DrawShadingCommand"/> shading, shared by
/// every command instance that draws the same shading under equivalent color conversion. Only the
/// field matching the shading's type is populated; the rest stay null.
/// </summary>
public sealed class ShadingCommandCacheEntry : ICommandCacheItem
{
    /// <summary>
    /// Function sample count used to build <see cref="Function"/>, <see cref="Axial"/>, <see cref="Radial"/>, or <see cref="Mesh"/>. Null when none of those were built.
    /// </summary>
    public int? FunctionSamples { get; set; }

    /// <summary>
    /// Tessellation vertex limit used to build <see cref="Mesh"/>. Null when it was not built.
    /// </summary>
    public int? TessellationVertices { get; set; }

    /// <summary>
    /// Sampled image and matrix built for a function-based (Type 1) shading. Null unless this entry holds a function-based shading.
    /// </summary>
    public FunctionShadingResult? Function { get; set; }

    /// <summary>
    /// Linear gradient built for an axial (Type 2) shading. Null unless this entry holds an axial shading.
    /// </summary>
    public PdfLinearGradient? Axial { get; set; }

    /// <summary>
    /// Radial gradient built for a radial (Type 3) shading. Null unless this entry holds a radial shading.
    /// </summary>
    public PdfRadialGradient? Radial { get; set; }

    /// <summary>
    /// Tessellated vertices built for a mesh shading: free-form or lattice-form Gouraud (Type 4 and
    /// Type 5), Coons or tensor-product patch mesh (Type 6 and Type 7). Null unless this entry holds
    /// one of those shading types.
    /// </summary>
    public PdfVertices? Mesh { get; set; }

    /// <summary>
    /// True when the built primitives were produced with the same sample and tessellation limits
    /// as <paramref name="parameters"/>. A mismatch means the entry must be rebuilt.
    /// </summary>
    public bool ParametersMatches(PdfCommandExecutionParameters parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        return (!FunctionSamples.HasValue || FunctionSamples.Value == parameters.DefaultFunctionSamples)
            && (!TessellationVertices.HasValue || TessellationVertices.Value == parameters.MaxTessellationVertices);
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
