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
        // Skip invisible annotations
        if (annotation.Flags.HasFlag(PdfAnnotationFlags.Invisible) || 
            annotation.Flags.HasFlag(PdfAnnotationFlags.Hidden))
        {
            return;
        }

        // Skip no-view annotations (unless printing)
        if (annotation.Flags.HasFlag(PdfAnnotationFlags.NoView) && !renderingParameters.PrintMode)
        {
            return;
        }

        // Skip non-print annotations when printing
        if (!annotation.Flags.HasFlag(PdfAnnotationFlags.Print) && renderingParameters.PrintMode)
        {
            return;
        }

        canvas.Save();

        try
        {
            // Render bubble indicator if applicable
            if (annotation.ShouldDisplayBubble)
            {
                PdfAnnotationBubbleRenderer.RenderBubble(canvas, annotation, _page, visualStateKind);
            }

            // Try to render using appearance dictionary first
            if (annotation.AppearanceDictionary != null && RenderAnnotationAppearance(canvas, annotation, renderingParameters, visualStateKind))
            {
                return; // Successfully rendered with appearance stream
            }

            // Try annotation-specific fallback rendering
            var fallbackPicture = annotation.CreateFallbackRender(_page, visualStateKind);
            if (fallbackPicture != null)
            {
                canvas.DrawPicture(fallbackPicture);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Render annotation using its appearance dictionary (/AP entry).
    /// Supports Form XObjects, Image XObjects, and other XObject subtypes.
    /// </summary>
    private bool RenderAnnotationAppearance(
        SKCanvas canvas,
        PdfAnnotationBase annotation,
        PdfRenderingParameters renderingParameters,
        PdfAnnotationVisualStateKind visualStateKind)
    {
        if (annotation.AppearanceDictionary == null)
        {
            return false;
        }

        var effectiveState = ResolveVisualState(annotation, visualStateKind);
        var appearanceObject = GetAppearanceObjectForState(annotation.AppearanceDictionary, effectiveState);

        if (appearanceObject == null)
        {
            return false;
        }

        var xObject = PdfXObject.FromObject(appearanceObject);

        canvas.Save();

        var annotationRect = annotation.Rectangle;
        var success = false;

        switch (xObject.Subtype)
        {
            case PdfXObjectSubtype.Form:
                success = RenderFormAppearance(canvas, appearanceObject, annotationRect, renderingParameters);
                break;

            case PdfXObjectSubtype.Image:
                success = RenderImageAppearance(canvas, appearanceObject, annotationRect, renderingParameters);
                break;

            default:
                _logger.LogWarning("Unsupported XObject subtype '{XObjectSubtype}' in annotation appearance", xObject.Subtype);
                break;
        }

        canvas.Restore();
        return success;
    }

    /// <summary>
    /// Resolves the best available visual state for the annotation based on what's supported.
    /// </summary>
    private static PdfAnnotationVisualStateKind ResolveVisualState(
        PdfAnnotationBase annotation,
        PdfAnnotationVisualStateKind requestedState)
    {
        if ((annotation.SupportedVisualStates & requestedState) != 0)
        {
            return requestedState;
        }

        if ((annotation.SupportedVisualStates & PdfAnnotationVisualStateKind.Rollover) != 0 && 
            requestedState == PdfAnnotationVisualStateKind.Down)
        {
            return PdfAnnotationVisualStateKind.Rollover;
        }

        if ((annotation.SupportedVisualStates & PdfAnnotationVisualStateKind.Normal) != 0)
        {
            return PdfAnnotationVisualStateKind.Normal;
        }

        return PdfAnnotationVisualStateKind.None;
    }

    /// <summary>
    /// Gets the appearance object for the specified visual state from the appearance dictionary.
    /// </summary>
    private static PdfObject GetAppearanceObjectForState(
        PdfDictionary appearanceDictionary,
        PdfAnnotationVisualStateKind state)
    {
        return state switch
        {
            PdfAnnotationVisualStateKind.Normal => appearanceDictionary.GetObject(PdfTokens.NKey),
            PdfAnnotationVisualStateKind.Rollover => appearanceDictionary.GetObject(PdfTokens.RolloverKey),
            PdfAnnotationVisualStateKind.Down => appearanceDictionary.GetObject(PdfTokens.DownKey),
            _ => null
        };
    }

    /// <summary>
    /// Render a Form XObject appearance.
    /// </summary>
    private bool RenderFormAppearance(SKCanvas canvas, PdfObject formObject, SKRect annotationRect, PdfRenderingParameters renderingParameters)
    {
        var formXObject = PdfForm.FromXObject(formObject, _page);
        if (formXObject == null)
            return false;

        var appearanceBBox = formXObject.BBox;

        if (annotationRect != SKRect.Empty)
        {
            // Calculate scaling factors to fit appearance into annotation rectangle
            float scaleX = annotationRect.Width / appearanceBBox.Width;
            float scaleY = annotationRect.Height / appearanceBBox.Height;

            // Position at annotation rectangle and apply scaling
            canvas.Translate(annotationRect.Left, annotationRect.Top);
            canvas.Scale(scaleX, scaleY);
        }
        
        var state = new PdfGraphicsState(_page, new HashSet<uint>(), renderingParameters, externalTransform: null);
        _renderer.DrawForm(canvas, formXObject, state);
        
        return true;
    }

    /// <summary>
    /// Render an Image XObject appearance.
    /// </summary>
    private bool RenderImageAppearance(SKCanvas canvas, PdfObject imageObject, SKRect annotationRect, PdfRenderingParameters renderingParameters)
    {
        var pdfImage = PdfImage.FromXObject(imageObject, _page, PdfString.Empty, isSoftMask: false);
        if (pdfImage == null)
            return false;

        if (annotationRect != SKRect.Empty)
        {
            // Position at annotation rectangle and scale to fit
            canvas.Translate(annotationRect.Left, annotationRect.Top);
            canvas.Scale(annotationRect.Width, annotationRect.Height);
        }
        
        var state = new PdfGraphicsState(_page, new HashSet<uint>(), renderingParameters, externalTransform: null);
        _renderer.DrawImage(canvas, pdfImage, state);
        
        return true;
    }

    /// <summary>
    /// Get color for annotation rendering using proper color space conversion.
    /// </summary>
    private SKColor GetAnnotationColor(PdfAnnotationBase annotation)
    {
        return annotation.ResolveColor(_page, new SKColor(255, 165, 0));
    }
}