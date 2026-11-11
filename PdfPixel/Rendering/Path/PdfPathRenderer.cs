using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Path;

/// <summary>
/// Standard path renderer supporting stroke, fill and combined operations, including pattern paints.
/// </summary>
internal class PdfPathRenderer
{
    private readonly ILoggerFactory _factory;
    private readonly ILogger<PdfPathRenderer> _logger;

    /// <summary>
    /// Initializes the renderer with the logger factory.
    /// </summary>
    public PdfPathRenderer(ILoggerFactory loggerFactory)
    {
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<PdfPathRenderer>();
    }

    /// <summary>
    /// Draw a path using the specified paint operation and fill rule.
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
                PathFillRenderTarget fillTarget = new(path, state);
                PathStrokeRenderTarget strokeTarget = new(path, state);

                PdfCommandRecorder recorder = ImplicitKnockoutGroup.Begin(state);
                fillTarget.Render(recorder);
                strokeTarget.Render(recorder);
                ImplicitKnockoutGroup.Draw(processor, recorder, state);
                break;
            }
        }
    }
}
