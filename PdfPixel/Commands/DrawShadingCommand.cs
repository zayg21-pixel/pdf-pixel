using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Cache;
using PdfPixel.Shading;
using PdfPixel.Shading.Decoding;
using PdfPixel.Shading.Model;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Represents a PDF shading. <see cref="BuildEntry"/> builds the expensive data (function sampling,
/// mesh decoding, gradient construction) needed to draw it.
/// </summary>
public sealed class DrawShadingCommand : PdfCommand
{
    private readonly PdfShading _shading;
    private readonly PdfShadingBuilder _builder;
    private readonly Color.Sampling.ColorTransformSampler _sampler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DrawShadingCommand"/> class.
    /// </summary>
    /// <param name="context">Snapshot of graphics-state values captured at record time.</param>
    /// <param name="loggerFactory">Logger factory for diagnostic output.</param>
    public DrawShadingCommand(ShadingDecodingContext context, ILoggerFactory loggerFactory)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _shading = Context.Shading;
        _builder = new PdfShadingBuilder(loggerFactory);
        _sampler = Context.Converter.GetRgbaSampler(Context.RenderingIntent, Context.TransferFunction);
    }

    /// <summary>
    /// The context this command was constructed with.
    /// </summary>
    public ShadingDecodingContext Context { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawShading;

    /// <summary>
    /// Builds the rendering primitives for this shading, sampling the shading's function(s) and
    /// constructing the type-specific gradient, mesh, or image data.
    /// </summary>
    public ShadingCommandCacheEntry BuildEntry(PdfCommandExecutionContext executionContext)
    {
        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        ShadingCommandCacheEntry entry = new();

        switch (_shading.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                entry.FunctionSamples = executionContext.Parameters.DefaultFunctionSamples;
                entry.Function = _builder.BuildFunctionBased(
                    _shading,
                    _sampler,
                    executionContext.Parameters.DefaultFunctionSamples,
                    executionContext.ExecutionObserver);
                break;
            }
            case PdfShadingType.Axial:
            {
                entry.FunctionSamples = executionContext.Parameters.DefaultFunctionSamples;
                PdfShadingColorStops axialColorStops = _builder.BuildShadingColorsAndStops(_shading, _sampler, executionContext.Parameters.DefaultFunctionSamples);
                entry.Axial = _builder.BuildLinearGradient(_shading, axialColorStops);
                break;
            }
            case PdfShadingType.Radial:
            {
                entry.FunctionSamples = executionContext.Parameters.DefaultFunctionSamples;
                PdfShadingColorStops radialColorStops = _builder.BuildShadingColorsAndStops(_shading, _sampler, executionContext.Parameters.DefaultFunctionSamples);
                entry.Radial = _builder.BuildRadialGradient(_shading, radialColorStops);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                entry.FunctionSamples = executionContext.Parameters.DefaultFunctionSamples;
                entry.TessellationVertices = executionContext.Parameters.MaxTessellationVertices;
                MeshColorResolver colorResolver = new(_shading, _sampler, executionContext.Parameters.DefaultFunctionSamples);
                entry.Mesh = _builder.BuildMeshVertices(
                    _shading,
                    colorResolver,
                    executionContext.Parameters.MaxTessellationVertices,
                    executionContext.ExecutionObserver);
                break;
            }
        }

        return entry;
    }

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawShadingCommand)} {_shading.ShadingType}";
}
