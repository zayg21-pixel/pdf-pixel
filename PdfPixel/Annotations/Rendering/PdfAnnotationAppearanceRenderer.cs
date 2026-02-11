using PdfPixel.Annotations.Models;
using PdfPixel.Forms;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Provides utilities for rendering annotation appearance streams.
/// </summary>
internal static class PdfAnnotationAppearanceRenderer
{
    /// <summary>
    /// Renders the appearance stream for an annotation.
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="annotation">The annotation to render.</param>
    /// <param name="page">The PDF page containing the annotation.</param>
    /// <param name="visualStateKind">The visual state to render.</param>
    /// <param name="renderer">The renderer context.</param>
    /// <param name="renderingParameters">Rendering parameters.</param>
    /// <returns>True if the appearance stream was rendered successfully.</returns>
    public static bool RenderAppearanceStream(
        SKCanvas canvas,
        PdfAnnotationBase annotation,
        PdfPage page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters)
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

        var success = false;

        switch (xObject.Subtype)
        {
            case PdfXObjectSubtype.Form:
                success = RenderFormAppearance(canvas, appearanceObject, annotation.Rectangle, page, renderer, renderingParameters);
                break;

            case PdfXObjectSubtype.Image:
                success = RenderImageAppearance(canvas, appearanceObject, annotation.Rectangle, page, renderer, renderingParameters);
                break;
        }

        canvas.Restore();
        return success;
    }

    /// <summary>
    /// Resolves the best available visual state based on what's supported.
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
    /// Gets the appearance object for the specified visual state.
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
    /// Renders a Form XObject appearance.
    /// </summary>
    private static bool RenderFormAppearance(
        SKCanvas canvas,
        PdfObject formObject,
        SKRect annotationRect,
        PdfPage page,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters)
    {
        var formXObject = PdfForm.FromXObject(formObject, page);
        if (formXObject == null)
        {
            return false;
        }

        var appearanceBBox = formXObject.BBox;
        var matrix = formXObject.Matrix;

        var transformedBBox = matrix.MapRect(appearanceBBox);
        var alignmentMatrix = ComputeAlignmentMatrix(transformedBBox, annotationRect);

        canvas.Concat(in alignmentMatrix);

        var state = new PdfGraphicsState(page, new HashSet<uint>(), renderingParameters, externalTransform: null);
        renderer.DrawForm(canvas, formXObject, state);

        return true;
    }

    /// <summary>
    /// Computes matrix A that scales and translates the transformed appearance box
    /// to align with the annotation's rectangle.
    /// </summary>
    private static SKMatrix ComputeAlignmentMatrix(SKRect transformedBBox, SKRect annotationRect)
    {
        float scaleX = annotationRect.Width / transformedBBox.Width;
        float scaleY = annotationRect.Height / transformedBBox.Height;

        float translateX = annotationRect.Left - transformedBBox.Left * scaleX;
        float translateY = annotationRect.Top - transformedBBox.Top * scaleY;

        return SKMatrix.CreateScaleTranslation(scaleX, scaleY, translateX, translateY);
    }

    /// <summary>
    /// Renders an Image XObject appearance.
    /// </summary>
    private static bool RenderImageAppearance(
        SKCanvas canvas,
        PdfObject imageObject,
        SKRect annotationRect,
        PdfPage page,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters)
    {
        var pdfImage = PdfImage.FromXObject(imageObject, page, PdfString.Empty, isSoftMask: false);
        if (pdfImage == null)
        {
            return false;
        }

        if (annotationRect != SKRect.Empty)
        {
            canvas.Translate(annotationRect.Left, annotationRect.Top);
            canvas.Scale(annotationRect.Width, annotationRect.Height);
        }

        var state = new PdfGraphicsState(page, new HashSet<uint>(), renderingParameters, externalTransform: null);
        renderer.DrawImage(canvas, pdfImage, state);

        return true;
    }
}
