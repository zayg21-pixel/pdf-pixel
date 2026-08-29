using PdfPixel.Annotations.Models;
using PdfPixel.Commands.Model;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
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
        PdfObject? appearanceObject = annotation.Appearance?.GetStream(visualStateKind);

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

        processor.Process(SaveStateCommand.Instance);

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

        processor.Process(RestoreStateCommand.Instance);

        if (optionalContent != null)
        {
            processor.Process(new EndMarkedContentCommand());
        }

        return success;
    }

    /// <summary>
    /// Renders a Form XObject appearance.
    /// </summary>
    private static bool RenderFormAppearance(
        IPdfCommandProcessor processor,
        PdfObject formObject,
        in PdfRectangle annotationRect,
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

        PdfRectangle appearanceBBox = formXObject.BBox;
        PdfMatrix matrix = formXObject.Matrix;

        PdfRectangle transformedBBox = matrix.MapRect(appearanceBBox);
        PdfMatrix alignmentMatrix = ComputeAlignmentMatrix(transformedBBox, annotationRect);

        processor.Process(new ConcatMatrixCommand(alignmentMatrix));

        PdfGraphicsState state = new(page, new HashSet<uint>(), observer, renderingParameters);
        renderer.DrawForm(processor, formXObject, state);

        return true;
    }

    /// <summary>
    /// Computes matrix A that scales and translates the transformed appearance box
    /// to align with the annotation's rectangle.
    /// </summary>
    private static PdfMatrix ComputeAlignmentMatrix(in PdfRectangle transformedBBox, in PdfRectangle annotationRect)
    {
        float scaleX = annotationRect.Width / transformedBBox.Width;
        float scaleY = annotationRect.Height / transformedBBox.Height;

        float translateX = annotationRect.Left - (transformedBBox.Left * scaleX);
        float translateY = annotationRect.Top - (transformedBBox.Top * scaleY);

        return PdfMatrix.CreateScaleTranslation(scaleX, scaleY, translateX, translateY);
    }

    /// <summary>
    /// Renders an Image XObject appearance.
    /// </summary>
    private static bool RenderImageAppearance(
        IPdfCommandProcessor processor,
        PdfObject imageObject,
        in PdfRectangle annotationRect,
        IPdfPageInternal page,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfImage pdfImage = PdfImage.GetImage(imageObject);
        if (pdfImage == null)
        {
            return false;
        }

        if (!annotationRect.IsEmpty)
        {
            processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateTranslation(annotationRect.Left, annotationRect.Top)));
            processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateScale(annotationRect.Width, annotationRect.Height)));
        }

        PdfGraphicsState state = new(page, new HashSet<uint>(), observer, renderingParameters);
        renderer.DrawImage(processor, pdfImage, state);

        return true;
    }
}
