using System;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Shading.Model;
using PdfPixel.Color.Paint;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Utilities;

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
            return;
        }
        if (shading == null)
        {
            return;
        }

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, processor, state);
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
            processor.Process(new ClipRectCommand(shading.BBox.Value, SKClipOperation.Intersect));
        }

        if (shading.Background != null && shading.BBox.HasValue)
        {
            var colorSpace = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter);
            var backgroundColor = colorSpace.ToSrgb(shading.Background, state.RenderingIntent, state.FullTransferFunction);

            var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            using var rectPath = new SKPath();
            rectPath.AddRect(shading.BBox.Value);

            processor.Process(new DrawPathCommand(rectPath, backgroundPaint));
        }

        var context = new ShadingDecodingContext(state, shading);
        processor.Process(new PdfDrawShadingCommand(shading, context, _loggerFactory));
    }
}
