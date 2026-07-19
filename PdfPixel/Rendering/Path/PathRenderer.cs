using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Standard path renderer supporting stroke, fill and combined operations, including pattern paints
/// and soft mask application. Uses PathPatternPaintTarget to derive an outline path for pattern
/// rendering in both fill and stroke scenarios.
/// </summary>
public class PathRenderer : IPathRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _factory;
    private readonly ILogger<PathRenderer> _logger;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public PathRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PathRenderer>();
    }

    /// <summary>
    /// Draw a path using the specified paint operation and fill rule.
    /// Handles pattern paints, soft masks, and combined fill+stroke layering.
    /// Note: FlatnessTolerance from graphics state is ignored, as SkiaSharp does not support curve flattening control.
    /// </summary>
    public void DrawPath(IPdfCommandProcessor processor, SKPath path, PdfGraphicsState state, PdfPaintOperation operation)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (path?.IsEmpty != false)
        {
            return;
        }

        if (!state.RenderingParameters.RenderPaths)
        {
            return;
        }

        // FlatnessTolerance is ignored in SkiaSharp rendering.
        // See PDF spec 8.4.5: Most modern renderers ignore or clamp this value for performance.

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, state);
        softMaskScope.BeginDrawContent();
        DrawPathCore(processor, path, state, operation);
        softMaskScope.EndDrawContent();
    }

    /// <summary>
    /// Core path drawing logic for each paint operation.
    /// SaveLayer for FillAndStroke now uses the current clip region (no explicit bounds) simplifying logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawPathCore(IPdfCommandProcessor processor, SKPath path, PdfGraphicsState state, PdfPaintOperation operation)
    {
        switch (operation)
        {
            case PdfPaintOperation.Stroke:
            {
                using PathStrokeRenderTarget target = new(new SKPath(path), state);
                target.Render(processor);
                break;
            }
            case PdfPaintOperation.Fill:
            {
                using PathFillRenderTarget target = new(new SKPath(path), state);
                target.Render(processor);
                break;
            }
            case PdfPaintOperation.FillAndStroke:
            {
                bool overlapAffectsCompositing = state.FillAlpha < 1
                    || state.StrokeAlpha < 1
                    || state.BlendMode != PdfBlendMode.Normal;

                if (overlapAffectsCompositing)
                {
                    using SKPaint strokePaint = PdfPaintFactory.CreateStrokePaint(state);
                    SKPath strokeOutline = strokePaint.GetFillPath(path);

                    processor.Process(SaveStateCommand.Instance);
                    processor.Process(new ClipPathCommand(strokeOutline, SKClipOperation.Difference));

                    using PathFillRenderTarget clippedFillTarget = new(new SKPath(path), state);
                    clippedFillTarget.Render(processor);

                    processor.Process(RestoreStateCommand.Instance);
                }
                else
                {
                    using PathFillRenderTarget fillTarget = new(new SKPath(path), state);
                    fillTarget.Render(processor);
                }

                using PathStrokeRenderTarget strokeTarget = new(new SKPath(path), state);
                strokeTarget.Render(processor);

                break;
            }
        }
    }
}
