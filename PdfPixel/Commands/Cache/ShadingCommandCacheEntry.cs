using PdfPixel.Models;
using PdfPixel.Shading;
using PdfPixel.Shading.Model;
using SkiaSharp;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// Holds the built rendering primitives for one <see cref="DrawShadingCommand"/> shading, shared by
/// every command instance that draws the same shading under equivalent color conversion. Only the
/// field matching the shading's type is populated; the rest stay null.
/// </summary>
internal sealed class ShadingCommandCacheEntry : ICommandCacheItem
{
    /// <summary>
    /// Function sample count used to build <see cref="Function"/>, <see cref="Axial"/>, or <see cref="Radial"/>. Null when none of those were built.
    /// </summary>
    public int? FunctionSamples { get; set; }

    /// <summary>
    /// Tessellation vertex limit used to build <see cref="PatchMesh"/>. Null when it was not built.
    /// </summary>
    public int? TessellationVertices { get; set; }

    public FunctionShadingResult? Function { get; set; }

    public SKPaint? Axial { get; set; }

    public RadialShadingPaints? Radial { get; set; }

    public PdfVertices? Gouraud { get; set; }

    public PdfVertices? PatchMesh { get; set; }

    /// <summary>
    /// True when the built primitives were produced with the same sample and tessellation limits
    /// as <paramref name="parameters"/>. A mismatch means the entry must be rebuilt.
    /// </summary>
    public bool ParametersMatches(PdfCommandExecutionParameters parameters)
    {
        return (!FunctionSamples.HasValue || FunctionSamples.Value == parameters.DefaultFunctionSamples)
            && (!TessellationVertices.HasValue || TessellationVertices.Value == parameters.MaxTessellationVertices);
    }

    public void Dispose()
    {
        Function?.Dispose();
        Axial?.Dispose();
        Radial?.Dispose();
    }
}
