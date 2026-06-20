using System;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Shading.Model;
using PdfPixel.Color.Paint;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Utilities;
using PdfPixel.Color.ColorSpace;

namespace PdfPixel.Rendering.Shading;

/// <summary>
/// Draws shadings using parsed <see cref="PdfShading"/> model.
/// Applies soft mask scope once per shading draw.
/// </summary>
public class ShadingRenderer : IShadingRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ShadingRenderer> _logger;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public ShadingRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<ShadingRenderer>();
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

        if (!state.RenderingParameters.RenderShadings)
        {
            return;
        }

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state);
        softMaskScope.BeginDrawContent();

        DrawShadingCore(processor, shading, state);
    }

    /// <summary>
    /// Core shading dispatch logic without soft mask handling.
    /// Clips to bounding box, draws background, and emits a lazy shading command.
    /// </summary>
    private void DrawShadingCore(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        if (shading.BBox.HasValue)
        {
            processor.Process(new ClipPathCommand(shading.BBox.Value, SKClipOperation.Intersect));
        }

        if (shading.Background != null && shading.BBox.HasValue)
        {
            PdfColorSpaceConverter colorSpace = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter) ?? DeviceRgbConverter.Instance;
            SKColor backgroundColor = colorSpace.ToSrgb(shading.Background, state.RenderingIntent, state.FullTransferFunction);

            SKPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            SKPath rectPath = new();
            rectPath.AddRect(shading.BBox.Value);

            processor.Process(new DrawPathCommand(rectPath, backgroundPaint));
        }

        ShadingDecodingContext context = new(state, shading);
        processor.Process(new PdfDrawShadingCommand(shading, context, _loggerFactory));
    }
}
