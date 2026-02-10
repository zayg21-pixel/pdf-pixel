using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Color.ColorSpace;
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
    public void RenderAnnotations(SKCanvas canvas, PdfRenderingParameters renderingParameters)
    {
        if (_page.Annotations.Count == 0)
            return;

        foreach (var annotation in _page.Annotations)
        {
            try
            {
                RenderAnnotation(canvas, annotation, renderingParameters);
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
    private void RenderAnnotation(SKCanvas canvas, PdfAnnotationBase annotation, PdfRenderingParameters renderingParameters)
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
            // Try to render using appearance dictionary first
            if (annotation.AppearanceDictionary != null && RenderAnnotationAppearance(canvas, annotation, renderingParameters))
            {
                return; // Successfully rendered with appearance stream
            }

            // Try annotation-specific fallback rendering
            var fallbackPicture = annotation.CreateFallbackRender(_page);
            if (fallbackPicture != null)
            {
                // Position and draw the fallback picture
                canvas.Translate(annotation.Rectangle.Left, annotation.Rectangle.Top);
                canvas.DrawPicture(fallbackPicture);
                return;
            }

            // Final fallback to default rendering
            RenderAnnotationDefault(canvas, annotation);
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
    private bool RenderAnnotationAppearance(SKCanvas canvas, PdfAnnotationBase annotation, PdfRenderingParameters renderingParameters)
    {
        if (annotation.AppearanceDictionary == null)
            return false;

        // Get normal appearance (could extend to support other states like Down, Rollover)
        var normalAppearance = annotation.AppearanceDictionary.GetObject(PdfTokens.NKey);
        if (normalAppearance == null)
            return false;

        // Create XObject wrapper to get strongly-typed subtype
        var xObject = PdfXObject.FromObject(normalAppearance);
        
        canvas.Save();
        
        // Position and scale the appearance to fit the annotation rectangle
        var annotationRect = annotation.Rectangle;
        var success = false;

        switch (xObject.Subtype)
        {
            case PdfXObjectSubtype.Form:
                success = RenderFormAppearance(canvas, normalAppearance, annotationRect, renderingParameters);
                break;

            case PdfXObjectSubtype.Image:
                success = RenderImageAppearance(canvas, normalAppearance, annotationRect, renderingParameters);
                break;

            default:
                _logger.LogWarning("Unsupported XObject subtype '{XObjectSubtype}' in annotation appearance", xObject.Subtype);
                break;
        }
        
        canvas.Restore();
        return success;
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
    /// Default rendering for annotations without appearance streams.
    /// </summary>
    private void RenderAnnotationDefault(SKCanvas canvas, PdfAnnotationBase annotation)
    {
        var rect = annotation.Rectangle;
        
        // Skip rendering if rectangle is too small to be visible
        if (rect.Width < 1 || rect.Height < 1)
            return;
        
        canvas.Save();
        
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = GetAnnotationColor(annotation),
            StrokeWidth = Math.Max(1.0f, Math.Min(rect.Width, rect.Height) * 0.02f), // Scale stroke with annotation size
            PathEffect = SKPathEffect.CreateDash([3, 3], 0) // Dashed outline
        };

        canvas.DrawRect(rect, paint);

        // For text annotations, draw the icon
        if (annotation is PdfTextAnnotation textAnnotation)
        {
            RenderTextAnnotationIcon(canvas, textAnnotation, rect);
        }
        
        canvas.Restore();
    }

    /// <summary>
    /// Get color for annotation rendering using proper color space conversion.
    /// </summary>
    private SKColor GetAnnotationColor(PdfAnnotationBase annotation)
    {
        // Use the new ResolveColor method from the base class with orange/yellow default
        return annotation.ResolveColor(_page, new SKColor(255, 165, 0)); // TODO: just for tests, not complient
    }

    /// <summary>
    /// Render a simple icon for text annotations.
    /// </summary>
    private void RenderTextAnnotationIcon(SKCanvas canvas, PdfTextAnnotation textAnnotation, SKRect rect)
    {
        var iconSize = Math.Min(rect.Width, rect.Height) * 0.8f;
        var iconRect = new SKRect(
            rect.Left + (rect.Width - iconSize) / 2,
            rect.Top + (rect.Height - iconSize) / 2,
            rect.Left + (rect.Width + iconSize) / 2,
            rect.Top + (rect.Height + iconSize) / 2
        );

        using var iconPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = GetAnnotationColor(textAnnotation)
        };

        // Simple note icon (could be enhanced based on IconNameWithDefault)
        canvas.DrawOval(iconRect, iconPaint);
        
        // Add a small text indicator
        using var textPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            TextSize = iconSize * 0.4f,
            TextAlign = SKTextAlign.Center,
            Typeface = SKTypeface.Default
        };

        var textY = iconRect.MidY + textPaint.TextSize / 3;
        canvas.DrawText("?", iconRect.MidX, textY, textPaint);
    }
}