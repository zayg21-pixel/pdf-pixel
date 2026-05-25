using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

public static class PdfDocumentContentExtensions
{
    public static PdfCommandRecorder GetAnnotationRecording(
        this IPdfDocument document,
        int pageNumber,
        double scale,
        PdfAnnotationBase activeAnnotation,
        PdfPanelPointerState pointerState,
        IPdfExecutionObserver observer)
    {
        var pdfPage = document.Pages[pageNumber - 1];

        if (pdfPage.Annotations.Count == 0)
        {
            return null;
        }

        var visualStateKind = ConvertToVisualStateKind(pointerState);

        var recorder = new PdfCommandRecorder();

        ApplyPageTransformations(pdfPage, recorder);

        var parameters = new PdfRenderingParameters { ScaleFactor = (float)scale };
        pdfPage.RenderAnnotations(recorder, parameters, activeAnnotation, visualStateKind, observer);

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
        var pdfPage = document.Pages[pageNumber - 1];

        var commandRecording = new PdfCommandRecorder();

        ApplyPageTransformations(pdfPage, commandRecording);

        pdfPage.Draw(commandRecording, observer);

        return commandRecording;
    }

    public static SKPicture RecordingToSkPicture(PdfPanelPageInfo pageInfo, PdfCommandRecorder commandRecording, PdfCommandExecutionContext executionContext)
    {
        if (commandRecording == null)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(SKRect.Create(pageInfo.Width, pageInfo.Height));

        commandRecording.Replay(canvas, Array.Empty<IPdfCommandModifier>(), executionContext);

        canvas.Flush();

        return recorder.EndRecording();
    }

    private static void ApplyPageTransformations(IPdfPage pdfPage, IPdfCommandProcessor commandRecording)
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
        var pdfPage = document.Pages[pageNumber - 1];
        string label = document.Pages[pageNumber - 1].PageLabel.DecodePdfString();
        return new PdfPanelPageInfo(label, pdfPage.CropBox.Width, pdfPage.CropBox.Height, pdfPage.Rotation);
    }
}
