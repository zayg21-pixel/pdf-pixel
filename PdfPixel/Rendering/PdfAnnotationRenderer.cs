using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Forms;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Rendering;

/// <summary>
/// Handles rendering of PDF annotations using appearance streams or default rendering fallbacks.
/// </summary>
public class PdfAnnotationRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly PdfPage _page;
    private readonly ILogger<PdfAnnotationRenderer> _logger;

    public PdfAnnotationRenderer(IPdfRenderer renderer, PdfPage page)
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
        SKCanvas canvas,
        PdfRenderingParameters renderingParameters,
        PdfAnnotationBase activeAnnotation,
        PdfAnnotationVisualStateKind visualStateKind)
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

                RenderAnnotation(canvas, annotation, renderingParameters, effectiveState);
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
    private void RenderAnnotation(
        SKCanvas canvas,
        PdfAnnotationBase annotation,
        PdfRenderingParameters renderingParameters,
        PdfAnnotationVisualStateKind visualStateKind)
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

        annotation.Render(canvas, _page, visualStateKind, _renderer, renderingParameters);
    }
}