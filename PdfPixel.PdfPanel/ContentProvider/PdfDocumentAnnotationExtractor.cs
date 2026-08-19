using PdfPixel.Annotations.Models;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.ContentProvider;

internal static class PdfDocumentAnnotationExtractor
{
    public static PdfAnnotationPopup? GetActiveAnnotation(PdfPanelPage page, PdfPoint pagePosition)
    {
        if (page == null)
        {
            return null;
        }

        return page.Popups.FirstOrDefault(x =>
            x.IsInteractive
                && page.FromPdfRect(x.HoverRectangle).Contains(pagePosition));
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

        HashSet<PdfReference> pageReferences = BuildPageReferences(pdfPage);
        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent = BuildRepliesByParent(pdfPage);
        List<PdfAnnotationPopup> popups = [];
        HashSet<PdfAnnotationBase> processedAnnotations = [];

        foreach (PdfPageAnnotation pageAnnotation in pdfPage.Annotations)
        {
            PdfAnnotationBase annotation = pageAnnotation.Content;

            if (processedAnnotations.Contains(annotation) || !IsThreadRoot(annotation, pageReferences))
            {
                continue;
            }

            PdfAnnotationMessage[] thread = BuildThread(annotation, repliesByParent, processedAnnotations);
            popups.Add(new PdfAnnotationPopup(pageAnnotation, thread));
        }

        return popups.ToArray();
    }

    /// <summary>
    /// Whether <paramref name="annotation"/> starts a thread: it either replies to nothing, or replies
    /// to an annotation that <paramref name="pageReferences"/> does not contain.
    /// </summary>
    private static bool IsThreadRoot(PdfAnnotationBase annotation, HashSet<PdfReference> pageReferences)
    {
        if (!annotation.InReplyTo.HasValue)
        {
            return true;
        }

        return !pageReferences.Contains(annotation.InReplyTo.Value);
    }

    /// <summary>
    /// Builds the message tree rooted at <paramref name="annotation"/>. An annotation carrying no
    /// contents produces no message of its own and its replies take its place.
    /// </summary>
    private static PdfAnnotationMessage[] BuildThread(
        PdfAnnotationBase annotation,
        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        if (!processedAnnotations.Add(annotation))
        {
            return Array.Empty<PdfAnnotationMessage>();
        }

        List<PdfAnnotationMessage> replies = [];

        foreach (PdfAnnotationBase reply in FindDirectReplies(annotation, repliesByParent))
        {
            replies.AddRange(BuildThread(reply, repliesByParent, processedAnnotations));
        }

        PdfAnnotationMessage? message = CreateAnnotationMessage(annotation, replies.ToArray());

        if (message == null)
        {
            return replies.ToArray();
        }

        return new PdfAnnotationMessage[] { message };
    }

    /// <summary>
    /// Collects the references of every annotation on the page.
    /// </summary>
    private static HashSet<PdfReference> BuildPageReferences(IPdfPage pdfPage)
    {
        HashSet<PdfReference> pageReferences = [];

        foreach (PdfPageAnnotation pageAnnotation in pdfPage.Annotations)
        {
            PdfAnnotationBase annotation = pageAnnotation.Content;

            if (annotation.Reference.IsValid)
            {
                pageReferences.Add(annotation.Reference);
            }
        }

        return pageReferences;
    }

    /// <summary>
    /// Groups the page's annotations by the annotation they reply to, so that the replies of a given
    /// annotation are found by a single lookup instead of a scan over every annotation on the page.
    /// </summary>
    private static Dictionary<PdfReference, List<PdfAnnotationBase>> BuildRepliesByParent(IPdfPage pdfPage)
    {
        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent = [];

        foreach (PdfPageAnnotation pageAnnotation in pdfPage.Annotations)
        {
            PdfAnnotationBase annotation = pageAnnotation.Content;

            if (!annotation.Reference.IsValid || !annotation.InReplyTo.HasValue)
            {
                continue;
            }

            PdfReference parentReference = annotation.InReplyTo.Value;

            if (!repliesByParent.TryGetValue(parentReference, out List<PdfAnnotationBase>? replies))
            {
                replies = new List<PdfAnnotationBase>();
                repliesByParent[parentReference] = replies;
            }

            replies.Add(annotation);
        }

        return repliesByParent;
    }

    /// <summary>
    /// Creates annotation message from an annotation's metadata.
    /// </summary>
    private static PdfAnnotationMessage? CreateAnnotationMessage(PdfAnnotationBase annotation, PdfAnnotationMessage[] replies)
    {
        if (annotation.Contents == null)
        {
            return null;
        }

        string contents = annotation.Contents.Value.DecodePdfString();

        if (string.IsNullOrEmpty(contents))
        {
            return null;
        }

        string? title = annotation.Title?.DecodePdfString();
        string? messageTitle = (!string.IsNullOrEmpty(title)) ? title : null;
        DateTimeOffset? messageDate = (annotation.CreationDate.HasValue) ? new DateTimeOffset(annotation.CreationDate.Value) : (DateTimeOffset?)null;

        return new PdfAnnotationMessage(messageDate, messageTitle, contents, replies);
    }

    private static List<PdfAnnotationBase> FindDirectReplies(
        PdfAnnotationBase annotation,
        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent)
    {
        if (!annotation.Reference.IsValid)
        {
            return new List<PdfAnnotationBase>();
        }

        if (!repliesByParent.TryGetValue(annotation.Reference, out List<PdfAnnotationBase>? replies))
        {
            return new List<PdfAnnotationBase>();
        }

        return replies;
    }
}
