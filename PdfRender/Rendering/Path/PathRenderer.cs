using Microsoft.Extensions.Logging;
using PdfRender.Color.Paint;
using PdfRender.Rendering.State;
using PdfRender.Transparency.Utilities;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfRender.Rendering.Path;

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
    public void DrawPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation)
    {
        if (canvas == null)
        {
            return;
        }

        if (path == null || path.IsEmpty)
        {
            return;
        }

        // FlatnessTolerance is ignored in SkiaSharp rendering.
        // See PDF spec 8.4.5: Most modern renderers ignore or clamp this value for performance.

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, state);
        softMaskScope.BeginDrawContent();
        DrawPathCore(canvas, path, state, operation);
        softMaskScope.EndDrawContent();
    }

    /// <summary>
    /// Core path drawing logic for each paint operation.
    /// SaveLayer for FillAndStroke now uses the current clip region (no explicit bounds) simplifying logic.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawPathCore(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation)
    {
        switch (operation)
        {
            case PaintOperation.Stroke:
            {
                using var target = new PathStrokeRenderTarget(path, state);
                target.Render(canvas);
                break;
            }
            case PaintOperation.Fill:
            {
                using var target = new PathFillRenderTarget(path, state);
                target.Render(canvas);
                break;
            }
            case PaintOperation.FillAndStroke:
            {
                // Fill phase.
                using var fillTarget = new PathFillRenderTarget(path, state);
                fillTarget.Render(canvas);

                // Stroke phase.
                using var strokeTarget = new PathStrokeRenderTarget(path, state);
                strokeTarget.Render(canvas);
                break;
            }
        }
    }
}