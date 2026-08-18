using PdfPixel.Color.Sampling;
using PdfPixel.Geometry;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Decoding;

namespace PdfPixel.Shading.Model;

/// <summary>
/// Holds the rendering primitives built for one shading. Only the property matching the shading's
/// type is populated; the rest stay null.
/// </summary>
public sealed class PdfShadingContent
{
    private readonly PdfRectangle? _bbox;

    private PdfRectangle? _bounds;
    private bool _boundsCalculated;

    private PdfShadingContent(PdfShadingType shadingType, PdfRectangle? bbox)
    {
        ShadingType = shadingType;
        _bbox = bbox;
    }

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
    /// Returns the area these primitives paint in shading space, or null when they paint everywhere
    /// they are drawn through.
    /// </summary>
    public PdfRectangle? GetBounds()
    {
        if (!_boundsCalculated)
        {
            _bounds = CalculateBounds();
            _boundsCalculated = true;
        }

        return _bounds;
    }

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
        PdfShadingContent content = new(shading.ShadingType, shading.BBox);

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

    private PdfRectangle? CalculateBounds()
    {
        PdfRectangle? bounds = _bbox;
        PdfRectangle? primitiveBounds = GetPrimitiveBounds();

        if (primitiveBounds != null)
        {
            bounds = (bounds != null) ? PdfRectangle.Intersect(bounds.Value, primitiveBounds.Value) : primitiveBounds;
        }

        return bounds;
    }

    private PdfRectangle? GetPrimitiveBounds()
    {
        switch (ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                if (Function == null)
                {
                    return null;
                }

                return Function.Matrix.MapRect(new PdfRectangle(0, 0, Function.Image.Width, Function.Image.Height));
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                if (Mesh == null)
                {
                    return null;
                }

                return PdfRectangle.FromPoints(Mesh.Positions);
            }
            default:
            {
                return null;
            }
        }
    }
}
