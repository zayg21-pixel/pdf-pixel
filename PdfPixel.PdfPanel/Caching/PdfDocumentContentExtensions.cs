using Microsoft.Extensions.Logging;
using PdfPixel.Annotations;
using PdfPixel.Skia;
using PdfPixel.Geometry;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Content;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.Commands.Execution;
using PdfPixel.Commands;
using PdfPixel.Content;
using PdfPixel.Imaging;

namespace PdfPixel.PdfPanel.Caching;

internal static class PdfDocumentContentExtensions
{
    public static PdfCommandRecorder? GetAnnotationRecording(
        this IPdfDocument document,
        int pageNumber,
        PagesDrawingRequest request,
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
            bool isActive = pageAnnotation == request.ActiveAnnotation?.PageAnnotation;

            if (!pageAnnotation.Content.IsVisible(isActive))
            {
                continue;
            }

            PdfAnnotationVisualStateKind visualStateKind = isActive
                ? ConvertToVisualStateKind(request.ActiveAnnotationState)
                : PdfAnnotationVisualStateKind.Normal;

            int rotation = request.GetPage(pageNumber).Rotation;

            recorder.Process(SaveStateCommand.Instance);
            recorder.Process(new ConcatMatrixCommand(pageAnnotation.Content.GetDisplayMatrix(request.Scale, request.ReferenceScale, rotation)));
            if (pageAnnotation.Content.ShouldDisplayBubble)
            {
                PdfPoint bubbleAnchor = pageAnnotation.Content.ContentStart;

                recorder.Process(SaveStateCommand.Instance);
                recorder.Process(new ConcatMatrixCommand(pageAnnotation.Content.GetBubbleMatrix(bubbleAnchor, request.Scale, request.ReferenceScale, rotation, pdfPage.CropBox)));
                pageAnnotation.RenderBubble(recorder, bubbleAnchor, visualStateKind);
                recorder.Process(RestoreStateCommand.Instance);
            }

            pageAnnotation.Render(recorder, visualStateKind, renderingParameters, observer);
            recorder.Process(RestoreStateCommand.Instance);
        }

        return recorder;
    }

    private static PdfAnnotationVisualStateKind ConvertToVisualStateKind(PdfPanelPointerState pointerState)
    {
        return pointerState switch
        {
            PdfPanelPointerState.Pressed => PdfAnnotationVisualStateKind.Down,
            PdfPanelPointerState.Hovered => PdfAnnotationVisualStateKind.Rollover,
            _ => PdfAnnotationVisualStateKind.Normal
        };
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
    /// Replays the command recording onto <paramref name="canvas"/>.
    /// The caller is responsible for creating the <see cref="SKPictureRecorder"/>,
    /// beginning recording, constructing the <see cref="PdfCommandExecutionContext"/>,
    /// and calling <see cref="SKPictureRecorder.EndRecording"/> after this method returns.
    /// </summary>
    public static async ValueTask RecordingToSkPictureAsync(
        PdfCommandRecorder commandRecording,
        PdfCommandExecutionContext executionContext,
        SKCanvas canvas,
        float pictureScale,
        ILoggerFactory loggerFactory)
    {
        if (commandRecording == null)
        {
            throw new ArgumentNullException(nameof(commandRecording));
        }

        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        SkCanvasCommandProcessor processor = new(canvas, executionContext, loggerFactory.CreateLogger<SkCanvasCommandProcessor>());

        await processor.ProcessAsync(new ConcatMatrixCommand(PdfMatrix.CreateScale(pictureScale, pictureScale))).ConfigureAwait(false);
        await commandRecording.ReplayAsync(processor).ConfigureAwait(false);

        canvas.Flush();
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
