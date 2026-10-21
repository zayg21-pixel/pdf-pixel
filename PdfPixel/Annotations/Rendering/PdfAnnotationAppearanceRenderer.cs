using PdfPixel.Annotations.Model;
using PdfPixel.Commands.Model;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
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
        PdfForm? appearanceForm = annotation.Appearance?.GetStream(visualStateKind);

        if (appearanceForm == null)
        {
            return false;
        }

        processor.Process(SaveStateCommand.Instance);

        RenderFormAppearance(processor, appearanceForm, annotation.Rectangle, annotation.BlendMode, page, renderer, renderingParameters, observer);

        processor.Process(RestoreStateCommand.Instance);

        return true;
    }

    /// <summary>
    /// Renders a Form XObject appearance.
    /// </summary>
    private static void RenderFormAppearance(
        IPdfCommandProcessor processor,
        PdfForm formXObject,
        in PdfRectangle annotationRect,
        PdfBlendMode? blendMode,
        IPdfPageInternal page,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfRectangle appearanceBoundingBox = formXObject.BoundingBox;
        PdfMatrix matrix = formXObject.Matrix;

        PdfRectangle transformedBoundingBox = matrix.MapRect(appearanceBoundingBox);
        PdfMatrix alignmentMatrix = ComputeAlignmentMatrix(transformedBoundingBox, annotationRect);

        processor.Process(new ConcatMatrixCommand(alignmentMatrix));

        PdfGraphicsState state = new(page, page.MediaBox, new HashSet<uint>(), observer, renderingParameters);

        if (blendMode != null)
        {
            state.FillPaint = state.FillPaint.WithBlendMode(blendMode.Value);
            state.StrokePaint = state.StrokePaint.WithBlendMode(blendMode.Value);
        }

        renderer.DrawForm(processor, formXObject, state);
    }

    /// <summary>
    /// Computes matrix A that scales and translates the transformed appearance box
    /// to align with the annotation's rectangle.
    /// </summary>
    private static PdfMatrix ComputeAlignmentMatrix(in PdfRectangle transformedBoundingBox, in PdfRectangle annotationRect)
    {
        float scaleX = annotationRect.Width / transformedBoundingBox.Width;
        float scaleY = annotationRect.Height / transformedBoundingBox.Height;

        float translateX = annotationRect.Left - (transformedBoundingBox.Left * scaleX);
        float translateY = annotationRect.Top - (transformedBoundingBox.Top * scaleY);

        return PdfMatrix.CreateScaleTranslation(scaleX, scaleY, translateX, translateY);
    }
}
