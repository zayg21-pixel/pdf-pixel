using System;
using SkiaSharp;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
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
    private readonly PdfShadingBuilder _shadingBuilder;

    public ShadingRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<ShadingRenderer>();
        _shadingBuilder = new PdfShadingBuilder(loggerFactory);
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
        softMaskScope.EndDrawContent();
    }

    /// <summary>
    /// Core shading dispatch logic without soft mask handling.
    /// Builds shading commands into a recorder, then replays through the processor.
    /// </summary>
    private void DrawShadingCore(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        var recorder = new PdfCommandRecorder();

        _shadingBuilder.Build(recorder, shading, state);

        if (recorder.Commands.Count == 0)
        {
            recorder.Dispose();
            return;
        }

        if (shading.BBox.HasValue)
        {
            processor.Process(new ClipRectCommand(shading.BBox.Value, SKClipOperation.Intersect, state.RenderingParameters.AntialiasClip));
        }

        if (shading.Background != null)
        {
            var colorSpace = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter);
            var backgroundColor = colorSpace.ToSrgb(shading.Background, state.RenderingIntent, state.FullTransferFunction);

            var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor, state);
            processor.Process(new DrawRectCommand(backgroundPaint));
        }

        processor.Process(new DrawRecordingCommand(recorder, new DefaultPdfCommandModifier()));
    }
}
