using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
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
        PdfAnnotationBase? activeAnnotation,
        PdfPanelPointerState pointerState,
        IPdfExecutionObserver observer)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];

        if (pdfPage.Annotations.Count == 0)
        {
            return null;
        }

        PdfCommandRecorder recorder = new();

        ApplyPageTransformations(pdfPage, recorder);

        foreach (PdfAnnotationBase annotation in pdfPage.Annotations)
        {
            if ((annotation.Flags & PdfAnnotationFlags.Invisible) != 0
                || (annotation.Flags & PdfAnnotationFlags.Hidden) != 0
                || (annotation.Flags & PdfAnnotationFlags.NoView) != 0)
            {
                continue;
            }

            PdfAnnotationVisualStateKind visualStateKind = (annotation == activeAnnotation)
                ? ConvertToVisualStateKind(pointerState)
                : PdfAnnotationVisualStateKind.Normal;

            pdfPage.RenderAnnotation(recorder, annotation, visualStateKind, observer);
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

    public static PdfCommandRecorder GeneratePageCommandRecording(this IPdfDocument document, int pageNumber, IPdfExecutionObserver observer)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];

        PdfCommandRecorder commandRecording = new();

        ApplyPageTransformations(pdfPage, commandRecording);

        pdfPage.RenderContent(commandRecording, observer);

        return commandRecording;
    }

    public static SKPicture? RecordingToSkPicture(in PdfPanelPageInfo pageInfo, PdfCommandRecorder? commandRecording, PdfCommandExecutionContext executionContext)
    {
        if (commandRecording == null)
        {
            return null;
        }

        using SKPictureRecorder recorder = new();
        using SKCanvas canvas = recorder.BeginRecording(SKRect.Create(pageInfo.Width, pageInfo.Height));

        commandRecording.Replay(canvas, Array.Empty<IPdfCommandModifier>(), executionContext);

        canvas.Flush();

        return recorder.EndRecording();
    }

    private static void ApplyPageTransformations(IPdfPage pdfPage, PdfCommandRecorder commandRecording)
    {
        commandRecording.Process(new ClipPathCommand(
            new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height),
            SKClipOperation.Intersect));

        commandRecording.Process(new ConcatMatrixCommand(
            SKMatrix.CreateTranslation(-pdfPage.CropBox.Left, pdfPage.CropBox.Height + pdfPage.CropBox.Top)));
        commandRecording.Process(new ConcatMatrixCommand(SKMatrix.CreateScale(1, -1)));
    }

    public static PdfPanelPageInfo GetPageInfo(IPdfDocument document, int pageNumber)
    {
        IPdfPage pdfPage = document.Pages[pageNumber - 1];
        string label = document.Pages[pageNumber - 1].PageLabel.DecodePdfString();
        return new PdfPanelPageInfo(label, pdfPage.CropBox.Width, pdfPage.CropBox.Height, pdfPage.Rotation);
    }
}
