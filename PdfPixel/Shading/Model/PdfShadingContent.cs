using PdfPixel.Color.Sampling;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Decoding;

namespace PdfPixel.Shading.Model;

/// <summary>
/// Holds the rendering primitives built for one shading. Only the property matching the shading's
/// type is populated; the rest stay null.
/// </summary>
public sealed class PdfShadingContent
{
    private PdfShadingContent(PdfShadingType shadingType) => ShadingType = shadingType;

    /// <summary>
    /// Type of the shading these primitives were built for.
    /// </summary>
    public PdfShadingType ShadingType { get; }

    /// <summary>
    /// Sampled image and matrix built for a function-based (Type 1) shading. Null unless this content
    /// holds a function-based shading.
    /// </summary>
    public FunctionShadingResult? Function { get; private set; }

    /// <summary>
    /// Linear gradient built for an axial (Type 2) shading. Null unless this content holds an axial
    /// shading.
    /// </summary>
    public PdfLinearGradient? Axial { get; private set; }

    /// <summary>
    /// Radial gradient built for a radial (Type 3) shading. Null unless this content holds a radial
    /// shading.
    /// </summary>
    public PdfRadialGradient? Radial { get; private set; }

    /// <summary>
    /// Tessellated vertices built for a mesh shading: free-form or lattice-form Gouraud (Type 4 and
    /// Type 5), Coons or tensor-product patch mesh (Type 6 and Type 7). Null unless this content holds
    /// one of those shading types.
    /// </summary>
    public PdfVertices? Mesh { get; private set; }

    /// <summary>
    /// Builds the primitives for a shading, sampling its function(s) through <paramref name="sampler"/>
    /// and constructing the type-specific gradient, mesh, or image data.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="sampler">Sampler every color of the shading is resolved through.</param>
    /// <param name="state">Graphics state the shading is drawn from.</param>
    internal static PdfShadingContent Build(PdfShading shading, ColorTransformSampler sampler, PdfGraphicsState state)
    {
        PdfShadingBuilder builder = new(state.Page.Document.LoggerFactory);
        int functionSamples = state.RenderingParameters.DefaultFunctionSamples;
        PdfShadingContent content = new(shading.ShadingType);

        switch (shading.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                content.Function = builder.BuildFunctionBased(shading, sampler, functionSamples, state.ExecutionObserver);
                break;
            }
            case PdfShadingType.Axial:
            {
                PdfShadingColorStops axialColorStops = builder.BuildShadingColorsAndStops(shading, sampler, functionSamples);
                content.Axial = builder.BuildLinearGradient(shading, axialColorStops);
                break;
            }
            case PdfShadingType.Radial:
            {
                PdfShadingColorStops radialColorStops = builder.BuildShadingColorsAndStops(shading, sampler, functionSamples);
                content.Radial = builder.BuildRadialGradient(shading, radialColorStops);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                MeshColorResolver colorResolver = new(shading, sampler, functionSamples);
                content.Mesh = builder.BuildMeshVertices(
                    shading,
                    colorResolver,
                    state.RenderingParameters.MaxTessellationVertices,
                    state.ExecutionObserver);
                break;
            }
        }

        return content;
    }
}
