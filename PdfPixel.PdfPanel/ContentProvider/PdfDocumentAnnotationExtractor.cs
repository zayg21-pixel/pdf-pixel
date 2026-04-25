using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.ContentProvider;

internal static class PdfDocumentAnnotationExtractor
{
    public static PdfAnnotationPopup[] CreateAnnotationPopups(this PdfDocument document, int pageNumber)
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
            var rect = FromPdfRect(pdfPage, annotation.GetHoverRectangle(pdfPage));
            popups.Add(new PdfAnnotationPopup(annotation, thread, rect));
        }

        return popups.ToArray();
    }

    private static PdfAnnotationMessage[] BuildAnnotationThread(
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

    private static Dictionary<PdfReference, PdfAnnotationBase> BuildAnnotationMap(PdfPage pdfPage)
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
        var title = annotation.Title.DecodePdfString();
        var contents = annotation.Contents.DecodePdfString();

        if (string.IsNullOrEmpty(contents))
        {
            return null;
        }

        var messageTitle = !string.IsNullOrEmpty(title) ? title : null;
        var messageDate = annotation.CreationDate.HasValue ? new DateTimeOffset(annotation.CreationDate.Value) : (DateTimeOffset?)null;

        return new PdfAnnotationMessage(messageDate, messageTitle, contents);
    }

    private static List<PdfAnnotationBase> FindAllReplies(
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

    private static List<PdfAnnotationBase> FindDirectReplies(
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
}
