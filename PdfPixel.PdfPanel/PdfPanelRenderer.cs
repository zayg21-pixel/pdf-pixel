using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Implements the IPdfRenderer interface.
/// </summary>
internal sealed class PdfPanelRenderer
{
    private readonly PdfDocument document;

    public PdfPanelRenderer(PdfDocument document)
    {
        this.document = document;
    }

    public PdfPanelPageInfo GetPageInfo(int pageNumber)
    {
        var pdfPage = document.Pages[pageNumber - 1];
        return new PdfPanelPageInfo(pdfPage.CropBox.Width, pdfPage.CropBox.Height, pdfPage.Rotation);
    }

    public PdfAnnotationPopup[] CreateAnnotationPopups(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > document.Pages.Count)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        var pdfPage = document.Pages[pageNumber - 1];
        if (pdfPage.Annotations.Count == 0)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        var annotationMap = BuildAnnotationMap(pdfPage);
        var popups = new List<PdfAnnotationPopup>();
        var processedAnnotations = new HashSet<PdfAnnotationBase>();

        foreach (var annotation in pdfPage.Annotations)
        {
            if (processedAnnotations.Contains(annotation))
            {
                continue;
            }

            if (annotation.InReplyTo.HasValue)
            {
                continue;
            }

            var thread = BuildAnnotationThread(annotation, annotationMap, processedAnnotations);
            var rect = FromPdfRect(pdfPage, annotation.HoverRectangle);
            popups.Add(new PdfAnnotationPopup(annotation, thread, rect));
        }

        return popups.ToArray();
    }

    private Dictionary<PdfReference, PdfAnnotationBase> BuildAnnotationMap(PdfPage pdfPage)
    {
        var map = new Dictionary<PdfReference, PdfAnnotationBase>();

        foreach (var annotation in pdfPage.Annotations)
        {
            var reference = annotation.AnnotationObject.Reference;
            if (reference.IsValid)
            {
                map[reference] = annotation;
            }
        }

        return map;
    }

    private PdfAnnotationMessage[] BuildAnnotationThread(
        PdfAnnotationBase rootAnnotation,
        Dictionary<PdfReference, PdfAnnotationBase> annotationMap,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        var messages = new List<PdfAnnotationMessage>();
        
        var rootMessage = CreateAnnotationMessage(rootAnnotation);
        if (rootMessage.HasValue)
        {
            messages.Add(rootMessage.Value);
        }
        processedAnnotations.Add(rootAnnotation);

        var replies = FindAllReplies(rootAnnotation, annotationMap, processedAnnotations);
        
        foreach (var reply in replies)
        {
            var replyMessage = CreateAnnotationMessage(reply);
            if (replyMessage.HasValue)
            {
                messages.Add(replyMessage.Value);
            }
        }

        return messages.ToArray();
    }

    private List<PdfAnnotationBase> FindAllReplies(
        PdfAnnotationBase annotation,
        Dictionary<PdfReference, PdfAnnotationBase> annotationMap,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        var replies = new List<PdfAnnotationBase>();
        var annotationRef = annotation.AnnotationObject.Reference;
        
        if (!annotationRef.IsValid)
        {
            return replies;
        }

        var directReplies = FindDirectReplies(annotationRef, annotationMap, processedAnnotations);
        
        foreach (var reply in directReplies)
        {
            replies.Add(reply);
            
            if (reply.ReplyType == PdfAnnotationReplyType.Reply)
            {
                var nestedReplies = FindAllReplies(reply, annotationMap, processedAnnotations);
                replies.AddRange(nestedReplies);
            }
        }

        return replies;
    }

    private List<PdfAnnotationBase> FindDirectReplies(
        PdfReference parentRef,
        Dictionary<PdfReference, PdfAnnotationBase> annotationMap,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        var replies = new List<PdfAnnotationBase>();

        foreach (var candidate in annotationMap.Values)
        {
            if (processedAnnotations.Contains(candidate))
            {
                continue;
            }

            if (candidate.InReplyTo.HasValue && candidate.InReplyTo.Value.Equals(parentRef))
            {
                processedAnnotations.Add(candidate);
                replies.Add(candidate);
            }
        }

        return replies;
    }

    /// <summary>
    /// Converts PDF rectangle coordinates to WPF coordinates.
    /// </summary>
    /// <param name="pdfPage">The PDF page for coordinate system reference.</param>
    /// <param name="pdfRect">Rectangle in PDF coordinates.</param>
    /// <returns>Rectangle in WPF coordinates.</returns>
    private static SKRect FromPdfRect(PdfPage pdfPage, SKRect pdfRect)
    {
        // PDF coordinate system: origin at bottom-left, Y increases upward
        // General coordinate system: origin at top-left, Y increases downward
        // Convert from PDF coordinates to general coordinates with proper Y-axis flip
        return SKRect.Create(
            pdfRect.Left - pdfPage.CropBox.Left, 
            pdfPage.CropBox.Height + pdfPage.CropBox.Top - pdfRect.Bottom,
            pdfRect.Width, 
            pdfRect.Height);
    }

    /// <summary>
    /// Creates annotation message from an annotation's metadata.
    /// </summary>
    private static PdfAnnotationMessage? CreateAnnotationMessage(PdfAnnotationBase annotation)
    {
        var title = annotation.Title.ToString();
        var contents = annotation.Contents.ToString();

        if (string.IsNullOrEmpty(contents))
        {
            return null;
        }

        var messageTitle = !string.IsNullOrEmpty(title) ? title : null;
        var messageDate = annotation.CreationDate.HasValue ? new DateTimeOffset(annotation.CreationDate.Value) : (DateTimeOffset?)null;

        return new PdfAnnotationMessage(messageDate, messageTitle, contents);
    }

    /// <summary>
    /// Apply page-level transformations for coordinate system conversion.
    /// This properly transforms from PDF coordinate system (bottom-left origin, Y-up) 
    /// to Skia coordinate system (top-left origin, Y-down).
    /// </summary>
    /// <param name="canvas">The canvas to apply transformations to.</param>
    /// <param name="pdfPage">The PDF page for coordinate system reference.</param>
    private static void ApplyPageTransformations(SKCanvas canvas, PdfPage pdfPage)
    {
        // Step 1: Translate to move origin from bottom-left to top-left with crop box offset
        canvas.Translate(-pdfPage.CropBox.Left, pdfPage.CropBox.Height + pdfPage.CropBox.Top);

        // Step 2: Flip Y-axis to convert from Y-up to Y-down
        // This will handle ALL coordinate transformations at once
        canvas.Scale(1, -1);
    }

    public SKPicture GetPicture(int pageNumber, double scale)
    {
        try
        {
            return GetPictureInternal(pageNumber, scale, previewMode: false);
        }
        catch
        {
            return null;
        }
    }

    public SKPicture GetAnnotationPicture(
        int pageNumber,
        double scale,
        SKPoint? pointerPosition,
        PdfPanelPointerState pointerState)
    {
        try
        {
            var pdfPage = document.Pages[pageNumber - 1];

            if (pdfPage.Annotations.Count == 0)
            {
                return null;
            }

            PdfAnnotationBase activeAnnotation = GetActiveAnnotation(pageNumber, pointerPosition);
            var visualStateKind = ConvertToVisualStateKind(pointerState);

            using var recorder = new SKPictureRecorder();
            using var canvas = recorder.BeginRecording(SKRect.Create(pdfPage.CropBox.Width, pdfPage.CropBox.Height));
            canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

            ApplyPageTransformations(canvas, pdfPage);

            var parameters = new PdfRenderingParameters { ScaleFactor = (float)scale, PreviewMode = false };
            pdfPage.RenderAnnotations(canvas, parameters, activeAnnotation, visualStateKind);

            canvas.Flush();
            var picture = recorder.EndRecording();

            return picture;
        }
        catch
        {
            return null;
        }
    }

    public PdfAnnotationBase GetActiveAnnotation(int pageNumber, SKPoint? pagePosition)
    {
        if (!pagePosition.HasValue)
        {
            return null;
        }

        if (pageNumber < 1 || pageNumber > document.Pages.Count)
        {
            return null;
        }

        var pdfPage = document.Pages[pageNumber - 1];
        if (pdfPage.Annotations.Count == 0)
        {
            return null;
        }

        foreach (var annotation in pdfPage.Annotations.Where(x => !x.InReplyTo.HasValue).OrderByDescending(x => x.ShouldDisplayBubble))
        {
            var pageRect = FromPdfRect(pdfPage, annotation.HoverRectangle);
            if (pageRect.Contains(pagePosition.Value))
            {
                return annotation;
            }
        }

        return null;
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

    public SKImage GetThumbnail(int pageNumber, int maxThumbnailSize)
    {
        try
        {
            var pdfPage = document.Pages[pageNumber - 1];
            var maxDimension = Math.Max(pdfPage.CropBox.Width, pdfPage.CropBox.Height);
            var scale = maxThumbnailSize / maxDimension;

            var width = (int)Math.Max(1, Math.Round(pdfPage.CropBox.Width * scale));
            var height = (int)Math.Max(1, Math.Round(pdfPage.CropBox.Height * scale));

            var bitmapInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(bitmapInfo);

            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)scale);
            canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

            ApplyPageTransformations(canvas, pdfPage);

            var parameters = new PdfRenderingParameters
            {
                ScaleFactor = (float)scale,
                PreviewMode = true
            };

            pdfPage.Draw(canvas, parameters);
            canvas.Flush();

            return SKImage.FromBitmap(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private SKPicture GetPictureInternal(int pageNumber, double scale, bool previewMode)
    {
        var pdfPage = document.Pages[pageNumber - 1];

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(SKRect.Create(pdfPage.CropBox.Width, pdfPage.CropBox.Height));
        canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

        ApplyPageTransformations(canvas, pdfPage);

        var parameters = new PdfRenderingParameters { ScaleFactor = (float)scale, PreviewMode = false };
        pdfPage.Draw(canvas, parameters);

        canvas.Flush();
        var picture = recorder.EndRecording();

        return picture;
    }

    public void Dispose()
    {
        document.Dispose();
    }
}
