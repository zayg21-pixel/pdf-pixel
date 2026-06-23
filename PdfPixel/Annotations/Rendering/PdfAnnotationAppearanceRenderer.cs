using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
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
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="annotation">The annotation to render.</param>
    /// <param name="page">The PDF page containing the annotation.</param>
    /// <param name="visualStateKind">The visual state to render.</param>
    /// <param name="renderer">The renderer context.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Observer for long-running operations.</param>
    /// <returns>True if the appearance stream was rendered successfully.</returns>
    public static bool RenderAppearanceStream(
        IPdfCommandProcessor processor,
        PdfAnnotationBase annotation,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        if (annotation.AppearanceDictionary == null)
        {
            return false;
        }

        PdfAnnotationVisualStateKind effectiveState = ResolveVisualState(annotation, visualStateKind);
        PdfObject? appearanceObject = GetAppearanceObjectForState(annotation.AppearanceDictionary, effectiveState);

        if (appearanceObject == null)
        {
            return false;
        }

        PdfXObject xObject = PdfXObject.FromObject(appearanceObject);

        PdfOptionalContentMembership? optionalContent = xObject.OptionalContent;
        if (optionalContent != null)
        {
            PdfMarkedContent markedContent = new(PdfTokens.OptionalContentKey) { OptionalContent = optionalContent };
            processor.Process(new BeginMarkedContentCommand(markedContent));
        }

        processor.Process(new SaveStateCommand());

        var success = false;

        switch (xObject.Subtype)
        {
            case PdfXObjectSubtype.Form:
            {
                success = RenderFormAppearance(processor, appearanceObject, annotation.Rectangle, page, renderer, renderingParameters, observer);
                break;
            }
            case PdfXObjectSubtype.Image:
            {
                success = RenderImageAppearance(processor, appearanceObject, annotation.Rectangle, page, renderer, renderingParameters, observer);
                break;
            }
        }

        processor.Process(new RestoreStateCommand());

        if (optionalContent != null)
        {
            processor.Process(new EndMarkedContentCommand());
        }

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

        if ((annotation.SupportedVisualStates & PdfAnnotationVisualStateKind.Rollover) != 0
            && requestedState == PdfAnnotationVisualStateKind.Down)
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
    private static PdfObject? GetAppearanceObjectForState(
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
        IPdfCommandProcessor processor,
        PdfObject formObject,
        SKRect annotationRect,
        IPdfPageInternal page,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfForm formXObject = PdfForm.FromXObject(formObject, page);
        if (formXObject == null)
        {
            return false;
        }

        SKRect appearanceBBox = formXObject.BBox;
        SKMatrix matrix = formXObject.Matrix;

        SKRect transformedBBox = matrix.MapRect(appearanceBBox);
        SKMatrix alignmentMatrix = ComputeAlignmentMatrix(transformedBBox, annotationRect);

        processor.Process(new ConcatMatrixCommand(alignmentMatrix));

        PdfGraphicsState state = new(page, new HashSet<uint>(), externalTransform: null, observer, renderingParameters);
        renderer.DrawForm(processor, formXObject, state);

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

        float translateX = annotationRect.Left - (transformedBBox.Left * scaleX);
        float translateY = annotationRect.Top - (transformedBBox.Top * scaleY);

        return SKMatrix.CreateScaleTranslation(scaleX, scaleY, translateX, translateY);
    }

    /// <summary>
    /// Renders an Image XObject appearance.
    /// </summary>
    private static bool RenderImageAppearance(
        IPdfCommandProcessor processor,
        PdfObject imageObject,
        SKRect annotationRect,
        IPdfPageInternal page,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfImage pdfImage = PdfImage.FromXObject(imageObject, page, PdfString.Empty, isSoftMask: false);
        if (pdfImage == null)
        {
            return false;
        }

        if (annotationRect != SKRect.Empty)
        {
            processor.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(annotationRect.Left, annotationRect.Top)));
            processor.Process(new ConcatMatrixCommand(SKMatrix.CreateScale(annotationRect.Width, annotationRect.Height)));
        }

        PdfGraphicsState state = new(page, new HashSet<uint>(), externalTransform: null, observer, renderingParameters);
        renderer.DrawImage(processor, pdfImage, state);

        return true;
    }
}
