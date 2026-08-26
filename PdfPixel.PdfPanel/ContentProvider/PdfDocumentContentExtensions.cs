using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Skia;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Threading.Tasks;
using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;

namespace PdfPixel.PdfPanel.ContentProvider;

internal static class PdfDocumentContentExtensions
{
    public static PdfCommandRecorder? GetAnnotationRecording(
        this IPdfDocument document,
        int pageNumber,
        PdfPageAnnotation? activeAnnotation,
        PdfPanelPointerState pointerState,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];

        if (pdfPage.Annotations.Count == 0)
        {
            return null;
        }

        PdfCommandRecorder recorder = new();

        recorder.ApplyPageTransformations(pdfPage.CropBox);

        foreach (PdfPageAnnotation pageAnnotation in pdfPage.Annotations)
        {
            if ((pageAnnotation.Content.Flags & PdfAnnotationFlags.Invisible) != 0
                || (pageAnnotation.Content.Flags & PdfAnnotationFlags.Hidden) != 0
                || (pageAnnotation.Content.Flags & PdfAnnotationFlags.NoView) != 0)
            {
                continue;
            }

            PdfAnnotationVisualStateKind visualStateKind = (pageAnnotation == activeAnnotation)
                ? ConvertToVisualStateKind(pointerState)
                : PdfAnnotationVisualStateKind.Normal;

            pageAnnotation.Render(recorder, visualStateKind, renderingParameters, observer);
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

        commandRecording.ApplyPageTransformations(pdfPage.CropBox);

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

    public static PdfPanelPageInfo GetPageInfo(IPdfDocument document, int pageNumber)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];
        string label = document.Pages[pageNumber - 1].PageLabel.DecodePdfString();
        return new PdfPanelPageInfo(label, pdfPage.CropBox, pdfPage.Rotation);
    }
}
