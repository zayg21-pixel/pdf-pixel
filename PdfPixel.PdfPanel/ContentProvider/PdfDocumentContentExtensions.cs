using Microsoft.Extensions.Logging;
using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

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

        PdfCommandRecorder recorder = new(document.LoggerFactory.CreateLogger<PdfCommandRecorder>());

        ApplyPageTransformations(pdfPage, recorder);

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

        PdfCommandRecorder commandRecording = new(document.LoggerFactory.CreateLogger<PdfCommandRecorder>());

        ApplyPageTransformations(pdfPage, commandRecording);

        pdfPage.Render(commandRecording, renderingParameters, observer);

        return commandRecording;
    }

    /// <summary>
    /// Replays the command recording onto the execution context's canvas.
    /// The caller is responsible for creating the <see cref="SKPictureRecorder"/>,
    /// beginning recording, constructing the <see cref="PdfCommandExecutionContext"/>
    /// with the resulting canvas, and calling <see cref="SKPictureRecorder.EndRecording"/>
    /// after this method returns.
    /// </summary>
    public static void RecordingToSkPicture(
        PdfCommandRecorder commandRecording,
        PdfCommandExecutionContext executionContext)
    {
        if (commandRecording == null)
        {
            throw new ArgumentNullException(nameof(commandRecording));
        }

        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        executionContext.Frames.Reset();
        commandRecording.Replay(executionContext);

        executionContext.Canvas.Flush();
    }

    private static void ApplyPageTransformations(IPdfPage pdfPage, PdfCommandRecorder commandRecording)
    {
        commandRecording.Process(new ClipRectangleCommand(
            new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height),
            SKClipOperation.Intersect));

        commandRecording.Process(new ConcatMatrixCommand(
            PdfMatrix.CreateTranslation(-pdfPage.CropBox.Left, pdfPage.CropBox.Height + pdfPage.CropBox.Top)));
        commandRecording.Process(new ConcatMatrixCommand(PdfMatrix.CreateScale(1, -1)));
    }

    public static PdfPanelPageInfo GetPageInfo(IPdfDocument document, int pageNumber)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];
        string label = document.Pages[pageNumber - 1].PageLabel.DecodePdfString();
        return new PdfPanelPageInfo(label, pdfPage.CropBox.Width, pdfPage.CropBox.Height, pdfPage.CropBox.Left, pdfPage.CropBox.Top, pdfPage.Rotation);
    }
}
