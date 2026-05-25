using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Rendering.State;
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
    public void DrawPath(IPdfCommandProcessor processor, SKPath path, PdfGraphicsState state, PaintOperation operation)
    {
        if (processor == null)
        {
            return;
        }

        if (path?.IsEmpty != false)
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
    private void DrawPathCore(IPdfCommandProcessor processor, SKPath path, PdfGraphicsState state, PaintOperation operation)
    {
        switch (operation)
        {
            case PaintOperation.Stroke:
            {
                using PathStrokeRenderTarget target = new(path, state);
                target.Render(processor);
                break;
            }
            case PaintOperation.Fill:
            {
                using PathFillRenderTarget target = new(path, state);
                target.Render(processor);
                break;
            }
            case PaintOperation.FillAndStroke:
            {
                    // Fill phase.
                    SKPath strokeOutline = PdfPaintFactory.CreateStrokePaint(state).GetFillPath(path);
                SKPath fillOutline;

                if (strokeOutline != null)
                {
                    fillOutline = path.Op(strokeOutline, SKPathOp.Difference);
                }
                else
                {
                    fillOutline = path;
                }

                using PathFillRenderTarget fillTarget = new(fillOutline, state);
                fillTarget.Render(processor);

                // Stroke phase.
                using PathStrokeRenderTarget strokeTarget = new(path, state);
                strokeTarget.Render(processor);

                break;
            }
        }
    }
}
