using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using System;

namespace PdfPixel.Rendering;

/// <summary>
/// Handles rendering of PDF annotations using appearance streams or default rendering fallbacks.
/// </summary>
public class PdfAnnotationRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly IPdfPageInternal _page;
    private readonly ILogger<PdfAnnotationRenderer> _logger;

    internal PdfAnnotationRenderer(IPdfRenderer renderer, IPdfPageInternal page)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _page = page ?? throw new ArgumentNullException(nameof(page));
        _logger = page.Document.LoggerFactory.CreateLogger<PdfAnnotationRenderer>();
    }

    /// <summary>
    /// Render all annotations for the page.
    /// Annotations are rendered on top of page content using their appearance streams or default rendering.
    /// </summary>
    public void RenderAnnotations(
        IPdfCommandProcessor processor,
        PdfRenderingParameters renderingParameters,
        PdfAnnotationBase activeAnnotation,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfExecutionObserver observer)
    {
        if (_page.Annotations.Count == 0)
        {
            return;
        }

        foreach (var annotation in _page.Annotations)
        {
            try
            {
                var effectiveState = annotation == activeAnnotation
                    ? visualStateKind
                    : PdfAnnotationVisualStateKind.Normal;

                RenderAnnotation(processor, annotation, renderingParameters, effectiveState, observer);

                observer?.Notify();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to render {AnnotationType} annotation", annotation.Subtype);
            }
        }
    }

    /// <summary>
    /// Render a single annotation using its appearance stream or default rendering.
    /// </summary>
    public void RenderAnnotation(
        IPdfCommandProcessor processor,
        PdfAnnotationBase annotation,
        PdfRenderingParameters renderingParameters,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfExecutionObserver observer)
    {
        if (annotation.Flags.HasFlag(PdfAnnotationFlags.Invisible) || 
            annotation.Flags.HasFlag(PdfAnnotationFlags.Hidden))
        {
            return;
        }

        if (annotation.Flags.HasFlag(PdfAnnotationFlags.NoView) && !renderingParameters.PrintMode)
        {
            return;
        }

        if (!annotation.Flags.HasFlag(PdfAnnotationFlags.Print) && renderingParameters.PrintMode)
        {
            return;
        }

        annotation.Render(processor, _page, visualStateKind, _renderer, renderingParameters, observer);
    }
}