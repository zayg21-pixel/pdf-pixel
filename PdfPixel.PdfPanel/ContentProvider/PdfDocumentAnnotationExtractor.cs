using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.ContentProvider;

internal static class PdfDocumentAnnotationExtractor
{
    public static PdfAnnotationPopup? GetActiveAnnotation(PdfAnnotationPopup[] popups, SKPoint pagePosition)
    {
        if (popups == null)
        {
            return null;
        }

        return popups.FirstOrDefault(x => x.IsInteractive && x.Rect.Contains(pagePosition));
    }

    public static PdfAnnotationPopup[] CreateAnnotationPopups(this IPdfDocument document, int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > document.Pages.Count)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        IPdfPage pdfPage = document.Pages[pageNumber - 1];
        if (pdfPage.Annotations.Count == 0)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        Dictionary<PdfReference, PdfAnnotationBase> annotationMap = BuildAnnotationMap(pdfPage);
        List<PdfAnnotationPopup> popups = [];
        HashSet<PdfAnnotationBase> processedAnnotations = [];

        foreach (PdfAnnotationBase annotation in pdfPage.Annotations)
        {
            if (processedAnnotations.Contains(annotation))
            {
                continue;
            }

            if (annotation.InReplyTo.HasValue)
            {
                continue;
            }

            PdfAnnotationMessage[] thread = BuildAnnotationThread(annotation, annotationMap, processedAnnotations);
            SKRect rect = FromPdfRect(pdfPage, annotation.GetHoverRectangle(pdfPage));
            PdfPanelAnnotationAction? action = ResolveAction(annotation);
            bool isInteractive = IsInteractive(annotation);
            popups.Add(new PdfAnnotationPopup(action, thread, rect, isInteractive) { Annotation = annotation });
        }

        return popups.ToArray();
    }

    private static bool IsInteractive(PdfAnnotationBase annotation)
    {
        if (annotation is PdfLinkAnnotation || annotation is PdfFileAttachmentAnnotation)
        {
            return true;
        }

        if (annotation.ShouldDisplayBubble)
        {
            return true;
        }

        if (annotation.SupportedVisualStates != PdfAnnotationVisualStateKind.Normal
            && annotation.SupportedVisualStates != PdfAnnotationVisualStateKind.None)
        {
            return true;
        }

        return false;
    }

    private static PdfAnnotationMessage[] BuildAnnotationThread(
        PdfAnnotationBase rootAnnotation,
        Dictionary<PdfReference, PdfAnnotationBase> annotationMap,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        List<PdfAnnotationMessage> messages = [];

        PdfAnnotationMessage? rootMessage = CreateAnnotationMessage(rootAnnotation);
        if (rootMessage.HasValue)
        {
            messages.Add(rootMessage.Value);
        }

        processedAnnotations.Add(rootAnnotation);

        List<PdfAnnotationBase> replies = FindAllReplies(rootAnnotation, annotationMap, processedAnnotations);

        foreach (PdfAnnotationBase reply in replies)
        {
            PdfAnnotationMessage? replyMessage = CreateAnnotationMessage(reply);
            if (replyMessage.HasValue)
            {
                messages.Add(replyMessage.Value);
            }
        }

        return messages.ToArray();
    }

    private static Dictionary<PdfReference, PdfAnnotationBase> BuildAnnotationMap(IPdfPage pdfPage)
    {
        Dictionary<PdfReference, PdfAnnotationBase> map = [];

        foreach (PdfAnnotationBase annotation in pdfPage.Annotations)
        {
            PdfReference reference = annotation.AnnotationObject.Reference;
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
    private static SKRect FromPdfRect(IPdfPage pdfPage, SKRect pdfRect)
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
        string title = annotation.Title.DecodePdfString();
        string contents = annotation.Contents.DecodePdfString();

        if (string.IsNullOrEmpty(contents))
        {
            return null;
        }

        string? messageTitle = (!string.IsNullOrEmpty(title)) ? title : null;
        DateTimeOffset? messageDate = (annotation.CreationDate.HasValue) ? new DateTimeOffset(annotation.CreationDate.Value) : (DateTimeOffset?)null;

        return new PdfAnnotationMessage(messageDate, messageTitle, contents);
    }

    private static List<PdfAnnotationBase> FindAllReplies(
        PdfAnnotationBase annotation,
        Dictionary<PdfReference, PdfAnnotationBase> annotationMap,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        List<PdfAnnotationBase> replies = [];
        PdfReference annotationRef = annotation.AnnotationObject.Reference;

        if (!annotationRef.IsValid)
        {
            return replies;
        }

        List<PdfAnnotationBase> directReplies = FindDirectReplies(annotationRef, annotationMap, processedAnnotations);

        foreach (PdfAnnotationBase reply in directReplies)
        {
            replies.Add(reply);

            if (reply.ReplyType == PdfAnnotationReplyType.Reply)
            {
                List<PdfAnnotationBase> nestedReplies = FindAllReplies(reply, annotationMap, processedAnnotations);
                replies.AddRange(nestedReplies);
            }
        }

        return replies;
    }

    private static PdfPanelAnnotationAction? ResolveAction(PdfAnnotationBase annotation)
    {
        if (annotation is not PdfLinkAnnotation link)
        {
            return null;
        }

        return PdfPanelAnnotationAction.FromLinkAnnotation(link);
    }

    private static List<PdfAnnotationBase> FindDirectReplies(
        in PdfReference parentRef,
        Dictionary<PdfReference, PdfAnnotationBase> annotationMap,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        List<PdfAnnotationBase> replies = [];

        foreach (PdfAnnotationBase candidate in annotationMap.Values)
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
