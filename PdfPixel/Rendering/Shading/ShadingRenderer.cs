using System;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Shading.Model;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Utilities;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Geometry;

namespace PdfPixel.Rendering.Shading;

/// <summary>
/// Draws shadings using parsed <see cref="PdfShading"/> model.
/// Applies soft mask scope once per shading draw.
/// </summary>
public class ShadingRenderer : IShadingRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILogger<ShadingRenderer> _logger;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public ShadingRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
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

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state, shading.BBox ?? state.GetUserSpaceClipBounds());
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
            processor.Process(new ClipRectangleCommand(shading.BBox.Value, PdfClipOperation.Intersect));
        }

        if (shading.Background != null && shading.BBox.HasValue)
        {
            PdfColorSpaceConverter colorSpace = state.Page.Cache.ColorSpace.Resolve(shading.ColorSpaceReference) ?? DeviceRgbConverter.Instance;
            PdfColor backgroundColor = colorSpace.ToSrgb(shading.Background, state.RenderingIntent, state.TransferFunction);

            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(shading.BBox.Value);

            processor.Process(new DrawPathCommand(rectPath.ToPath(), backgroundPaint));
        }

        processor.Process(new DrawShadingCommand(shading.GetContent(state), state.FillPaint.Alpha));
    }
}
