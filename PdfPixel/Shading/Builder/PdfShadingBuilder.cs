using Microsoft.Extensions.Logging;

namespace PdfPixel.Shading;


/// <summary>
/// Provides methods for building SkiaSharp rendering primitives from PDF shading models.
/// Supports function-based (type 1), axial (type 2), radial (type 3),
/// Gouraud (type 4/5), Coons (type 6), and tensor-product (type 7) shadings.
/// Methods are called lazily by <see cref="PdfPixel.Commands.DrawShadingCommand"/> at Execute time.
/// </summary>
internal partial class PdfShadingBuilder
{
    private readonly ILogger<PdfShadingBuilder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfShadingBuilder"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory for diagnostic output.</param>
    public PdfShadingBuilder(ILoggerFactory loggerFactory) => _logger = loggerFactory.CreateLogger<PdfShadingBuilder>();
}
