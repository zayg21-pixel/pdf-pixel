using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using SkiaSharp;

namespace PdfPixel.Shading;


/// <summary>
/// Provides methods for building <see cref="IPdfCommand"/> instances from PDF shading models.
/// Supports function-based (type 1), axial (type 2), radial (type 3),
/// Gouraud (type 4/5), Coons (type 6), and tensor-product (type 7) shadings.
/// </summary>
internal partial class PdfShadingBuilder
{
    private readonly ILogger<PdfShadingBuilder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingBuilder"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for diagnostic output.</param>
    public PdfShadingBuilder(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<PdfShadingBuilder>();
    }

    /// <summary>
    /// Builds shading commands for the given shading model and pushes them into the processor.
    /// </summary>
    /// <param name="processor">The command processor that receives generated commands.</param>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="state">Current graphics state.</param>
    public void Build(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        switch (shading.ShadingType)
        {
            case 1:
                BuildFunctionBasedCommand(processor, shading, state);
                break;
            case 2:
                BuildAxialCommand(processor, shading, state);
                break;
            case 3:
                BuildRadialCommand(processor, shading, state);
                break;
            case 4:
            case 5:
                BuildGouraudCommand(processor, shading, state);
                break;
            case 6:
                BuildType6Command(processor, shading, state);
                break;
            case 7:
                BuildType7Command(processor, shading, state);
                break;
            default:
                _logger.LogWarning("Shading type {ShadingType} is not supported", shading.ShadingType);
                break;
        }
    }
}
