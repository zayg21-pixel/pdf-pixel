using System;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using PdfPixel.Shading;
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
    public void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state)
    {
        if (canvas == null)
        {
            return;
        }
        if (shading == null)
        {
            return;
        }

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, state);
        softMaskScope.BeginDrawContent();
        DrawShadingCore(canvas, shading, state);
        softMaskScope.EndDrawContent();
    }

    /// <summary>
    /// Core shading dispatch logic without soft mask handling.
    /// Uses ToShader for both axial and radial shading. No special clipping for radial shading.
    /// </summary>
    private void DrawShadingCore(SKCanvas canvas, PdfShading shading, PdfGraphicsState state)
    {
        using var shaderPicture = PdfShadingBuilder.ToPicture(shading, state, canvas.DeviceClipBounds);

        if (shaderPicture == null)
        {
            _logger.LogWarning("Shading type " + shading.ShadingType + " not implemented or invalid shading data");
            return;
        }
        using var paint = PdfPaintFactory.CreateShadingPaint(state);

        if (shading.BBox.HasValue)
        {
            canvas.ClipRect(shading.BBox.Value, SKClipOperation.Intersect, antialias: !state.RenderingParameters.PreviewMode);
        }

        if (shading.Background != null)
        {
            var colorSpace = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter);
            var backgroundColor = colorSpace.ToSrgb(shading.Background, state.RenderingIntent, state.FullTransferFunction);

            using var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor, state);
            canvas.DrawRect(canvas.LocalClipBounds, backgroundPaint);
        }

        canvas.DrawPicture(shaderPicture, paint);
    }
}
