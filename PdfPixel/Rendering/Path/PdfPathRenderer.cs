using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Standard path renderer supporting stroke, fill and combined operations, including pattern paints
/// and soft mask application. Uses PathPatternPaintTarget to derive an outline path for pattern
/// rendering in both fill and stroke scenarios.
/// </summary>
internal class PdfPathRenderer
{
    private readonly PdfRenderer _renderer;
    private readonly ILoggerFactory _factory;
    private readonly ILogger<PdfPathRenderer> _logger;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public PdfPathRenderer(PdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PdfPathRenderer>();
    }

    /// <summary>
    /// Draw a path using the specified paint operation and fill rule.
    /// Handles pattern paints, soft masks, and combined fill+stroke layering.
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

        if (!state.RenderingParameters.ProcessPaths)
        {
            return;
        }

        if (state.RenderingParameters.ExtractContent)
        {
            PdfRectangle contentBounds = (operation == PdfPaintOperation.Fill)
                ? path.GetBounds()
                : path.GetStrokeBounds(state.StrokePaint);

            processor.Process(new PdfContentCommand<PdfPath>(PdfContentKind.Path, PdfMatrix.Identity, contentBounds, path));
        }

        if (!state.RenderingParameters.RenderContent)
        {
            return;
        }

        DrawPathCore(processor, path, state, operation);
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

                using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, target.Bounds);
                target.Render(processor);
                break;
            }
            case PdfPaintOperation.Fill:
            {
                PathFillRenderTarget target = new(path, state);

                using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, target.Bounds);
                target.Render(processor);
                break;
            }
            case PdfPaintOperation.FillAndStroke:
            {
                PathStrokeRenderTarget strokeTarget = new(path, state);

                using SoftMaskDrawingScope? softMaskScope = SoftMaskDrawingScope.Begin(_renderer, processor, state, strokeTarget.Bounds);

                bool overlapAffectsCompositing = state.FillPaint.RequiresComposition
                    || state.StrokePaint.RequiresComposition;

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

                strokeTarget.Render(processor);
                break;
            }
        }
    }
}
