using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
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
    public void DrawPath(IPdfCommandProcessor processor, PdfPath path, PdfGraphicsState state, PdfPaintOperation operation)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
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
    private void DrawPathCore(IPdfCommandProcessor processor, PdfPath path, PdfGraphicsState state, PdfPaintOperation operation)
    {
        switch (operation)
        {
            case PdfPaintOperation.Stroke:
            {
                PathStrokeRenderTarget target = new(path, state);
                target.Render(processor);
                break;
            }
            case PdfPaintOperation.Fill:
            {
                PathFillRenderTarget target = new(path, state);
                target.Render(processor);
                break;
            }
            case PdfPaintOperation.FillAndStroke:
            {
                bool overlapAffectsCompositing = state.FillPaint.Alpha < 1
                    || state.StrokePaint.Alpha < 1
                    || state.FillPaint.BlendMode != PdfBlendMode.Normal;

                if (overlapAffectsCompositing)
                {
                    processor.Process(SaveStateCommand.Instance);
                    processor.Process(new ClipPathCommand(path, PdfClipOperation.Difference, state.StrokePaint));

                    PathFillRenderTarget clippedFillTarget = new(path, state);
                    clippedFillTarget.Render(processor);

                    processor.Process(RestoreStateCommand.Instance);
                }
                else
                {
                    PathFillRenderTarget fillTarget = new(path, state);
                    fillTarget.Render(processor);
                }

                PathStrokeRenderTarget strokeTarget = new(path, state);
                strokeTarget.Render(processor);

                break;
            }
        }
    }
}
