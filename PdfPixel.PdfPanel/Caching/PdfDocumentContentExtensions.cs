using PdfPixel.Annotations;
using PdfPixel.Geometry;
using System.Collections.Generic;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Commands.Execution;
using PdfPixel.Commands;
using PdfPixel.Content;
using PdfPixel.Imaging;
using PdfPixel.OptionalContent;

namespace PdfPixel.PdfPanel.Caching;

internal static class PdfDocumentContentExtensions
{
    /// <summary>
    /// Records every annotation of the page once, as a <see cref="DrawAnnotationCommand"/> holding the appearance of
    /// each visual state it supports, drawn in the state the execution context gives it; <see langword="null"/> when the
    /// page has no annotations.
    /// </summary>
    public static PdfCommandRecorder? GetAnnotationRecording(
        this IPdfDocument document,
        int pageNumber,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];
        IReadOnlyList<PdfPageAnnotation> pageAnnotations = pdfPage.GetAnnotations();

        if (pageAnnotations.Count == 0)
        {
            return null;
        }

        PdfCommandRecorder recorder = new();

        recorder.ApplyPageTransformations(pdfPage.CropBox, scale: pdfPage.UserUnit);

        foreach (PdfPageAnnotation pageAnnotation in pageAnnotations)
        {
            PdfAnnotationVisualStateKind supportedStates = pageAnnotation.Content.SupportedStates;
            PdfCommandRecorder normal = RecordAppearance(pageAnnotation, PdfAnnotationVisualStateKind.Normal, renderingParameters, observer);
            PdfCommandRecorder? rollover = null;
            PdfCommandRecorder? down = null;

            if ((supportedStates & PdfAnnotationVisualStateKind.Rollover) != 0)
            {
                rollover = RecordAppearance(pageAnnotation, PdfAnnotationVisualStateKind.Rollover, renderingParameters, observer);
            }

            if ((supportedStates & PdfAnnotationVisualStateKind.Down) != 0)
            {
                down = RecordAppearance(pageAnnotation, PdfAnnotationVisualStateKind.Down, renderingParameters, observer);
            }

            recorder.Process(new DrawAnnotationCommand(pageAnnotation.Content, normal, rollover, down));
        }

        return recorder;
    }

    private static PdfCommandRecorder RecordAppearance(
        PdfPageAnnotation pageAnnotation,
        PdfAnnotationVisualStateKind visualState,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfCommandRecorder recorder = new();

        pageAnnotation.Render(recorder, visualState, renderingParameters, observer);

        return recorder;
    }

    /// <summary>
    /// Creates the execution context the content of <paramref name="page"/> is followed with, its content tree
    /// collecting the content for words and content items.
    /// </summary>
    /// <param name="page">The page whose content is followed.</param>
    /// <param name="optionalContentStates">
    /// The optional content states to follow the content with, or <see langword="null"/> for the document's default
    /// configuration.
    /// </param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    public static PdfCommandExecutionContext CreateContentExecutionContext(
        this PdfPanelPage page,
        PdfOptionalContentStates? optionalContentStates,
        IPdfExecutionObserver observer)
    {
        PdfOptionalContentStates statesToFollow;

        if (optionalContentStates != null)
        {
            statesToFollow = optionalContentStates;
        }
        else
        {
            statesToFollow = page.PanelDocument.Document.OptionalContentProperties.GetDefaultStates();
        }

        return new PdfCommandExecutionContext(
            page.PanelDocument.CommandCache,
            new PdfCommandExecutionParameters(),
            page.PanelDocument.DocumentLocker,
            statesToFollow,
            PdfAnnotationStates.Empty,
            observer);
    }

    /// <summary>
    /// Generates a command recording for the specified page.
    /// </summary>
    /// <param name="document">The document to read from.</param>
    /// <param name="pageNumber">1-based page number.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    public static PdfCommandRecorder GeneratePageCommandRecording(this IPdfDocument document, int pageNumber, PdfRenderingParameters renderingParameters, IPdfExecutionObserver observer)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];

        PdfCommandRecorder commandRecording = new();

        commandRecording.ApplyPageTransformations(pdfPage.CropBox, scale: pdfPage.UserUnit);

        pdfPage.Render(commandRecording, renderingParameters, observer);

        return commandRecording;
    }

    /// <summary>
    /// Creates the content items of <paramref name="entries"/> on the given 1-based page number,
    /// with bounding boxes mapped through <paramref name="pictureToPage"/>.
    /// </summary>
    public static PdfPanelContentItem[] CreateContentItems(int pageNumber, PdfContentEntry[] entries, in PdfMatrix pictureToPage)
    {
        var contentItems = new PdfPanelContentItem[entries.Length];

        for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
        {
            PdfContentEntry entry = entries[entryIndex];
            PdfRectangle boundingBox = pictureToPage.MapRect(entry.GetBounds());

            if (entry.Item.Command is PdfContentCommand<PdfImage> imageCommand)
            {
                contentItems[entryIndex] = new PdfPanelImageContentItem(pageNumber, entry, boundingBox, imageCommand.Content);
            }
            else
            {
                contentItems[entryIndex] = new PdfPanelContentItem(pageNumber, entry, boundingBox);
            }
        }

        return contentItems;
    }
}
