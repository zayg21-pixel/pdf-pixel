using System;
using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;
using PdfPixel.Commands;
using PdfPixel.Shading;
using PdfPixel.Transparency;

namespace PdfPixel.Rendering.Shading;

/// <summary>
/// Draws shadings using parsed <see cref="PdfShading"/> model.
/// Applies soft mask scope once per shading draw.
/// </summary>
internal class PdfShadingRenderer
{
    private readonly PdfRenderer _renderer;
    private readonly ILogger<PdfShadingRenderer> _logger;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public PdfShadingRenderer(PdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _logger = loggerFactory.CreateLogger<PdfShadingRenderer>();
    }

    /// <summary>
    /// Draw a shading fill described by a parsed shading model, applying soft mask if present.
    /// </summary>
    public void DrawShading(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (shading == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (!state.RenderingParameters.ProcessShadings)
        {
            return;
        }

        if (state.RenderingParameters.ExtractContent)
        {
            PdfRectangle shadingBounds = shading.BoundingBox ?? state.GetUserSpaceClipBounds();
            processor.Process(new PdfContentCommand<PdfShading>(PdfContentKind.Shading, PdfMatrix.Identity, shadingBounds, shading));
        }

        if (!state.RenderingParameters.RenderContent)
        {
            return;
        }

        PdfColorSpaceConverter colorSpace = state.Host.Resources.ResolveColorSpace(shading.ColorSpaceReference) ?? PdfDeviceRgbColorSpaceConverter.Instance;
        PdfShadingContent content = shading.GetContent(colorSpace, state);

        using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, content.GetBounds() ?? state.GetUserSpaceClipBounds());

        DrawShadingCore(processor, shading, colorSpace, state, content);
    }

    /// <summary>
    /// Core shading dispatch logic without soft mask handling.
    /// Clips to bounding box, draws background, and emits a lazy shading command.
    /// </summary>
    private static void DrawShadingCore(IPdfCommandProcessor processor, PdfShading shading, PdfColorSpaceConverter colorSpace, PdfGraphicsState state, PdfShadingContent content)
    {
        if (shading.BoundingBox.HasValue)
        {
            processor.Process(new ClipRectangleCommand(shading.BoundingBox.Value, PdfClipOperation.Intersect));
        }

        if (shading.Background != null && shading.BoundingBox.HasValue)
        {
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(state, colorSpace, shading.Background);

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(shading.BoundingBox.Value);

            processor.Process(new DrawPathCommand(rectPath.ToPath(), backgroundPaint));
        }

        PdfColorSpaceConverter contentColorSpace = PdfShadingContent.GetColorSpace(colorSpace);
        processor.Process(new DrawShadingCommand(content, state.FillPaint.WithColorSpace(contentColorSpace, state.Host.Resources.DeviceColorMapping)));
    }
}
