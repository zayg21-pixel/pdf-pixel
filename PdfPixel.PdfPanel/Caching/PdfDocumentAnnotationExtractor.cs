using PdfPixel.Annotations;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.PdfPanel.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Caching;

internal static class PdfDocumentAnnotationExtractor
{
    public static PdfAnnotationPopup? GetActiveAnnotation(PdfPanelPage page, PdfPoint pagePosition, float scale, float referenceScale)
    {
        if (page == null)
        {
            return null;
        }

        return page.Popups.FirstOrDefault(x =>
            x.IsInteractive
                && IsInHoverRectangles(page, x, pagePosition, scale, referenceScale));
    }

    public static PdfAnnotationPopup[] CreateAnnotationPopups(this PdfPanelDocument panelDocument, int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > panelDocument.Document.Pages.Count)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        IReadOnlyList<PdfPageAnnotation> pageAnnotations = panelDocument.Document.Pages[pageNumber - 1].GetAnnotations();
        if (pageAnnotations.Count == 0)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        HashSet<PdfReference> markupReferences = BuildMarkupReferences(pageAnnotations);
        Dictionary<PdfReference, List<PdfMarkupAnnotation>> repliesByParent = BuildRepliesByParent(pageAnnotations);
        List<PdfAnnotationPopup> popups = [];
        HashSet<PdfMarkupAnnotation> processedAnnotations = [];

        foreach (PdfPageAnnotation pageAnnotation in pageAnnotations)
        {
            if (pageAnnotation.Content is PdfMarkupAnnotation markupAnnotation)
            {
                if (processedAnnotations.Contains(markupAnnotation) || !IsThreadRoot(markupAnnotation, markupReferences))
                {
                    continue;
                }

                PdfAnnotationMessage[] thread = BuildThread(markupAnnotation, repliesByParent, processedAnnotations);
                popups.Add(new PdfAnnotationPopup(panelDocument, pageAnnotation, thread));
            }
            else
            {
                popups.Add(new PdfAnnotationPopup(panelDocument, pageAnnotation, Array.Empty<PdfAnnotationMessage>()));
            }
        }

        return popups.ToArray();
    }

    /// <summary>
    /// Whether <paramref name="annotation"/> starts a thread: it either replies to nothing, or replies
    /// to an annotation that <paramref name="markupReferences"/> does not contain.
    /// </summary>
    private static bool IsThreadRoot(PdfMarkupAnnotation annotation, HashSet<PdfReference> markupReferences)
    {
        if (annotation.InReplyTo == null)
        {
            return true;
        }

        return !markupReferences.Contains(annotation.InReplyTo.Value);
    }

    /// <summary>
    /// Builds the message tree rooted at <paramref name="annotation"/>. An annotation carrying no
    /// contents produces no message of its own and its replies take its place.
    /// </summary>
    private static PdfAnnotationMessage[] BuildThread(
        PdfMarkupAnnotation annotation,
        Dictionary<PdfReference, List<PdfMarkupAnnotation>> repliesByParent,
        HashSet<PdfMarkupAnnotation> processedAnnotations)
    {
        if (!processedAnnotations.Add(annotation))
        {
            return Array.Empty<PdfAnnotationMessage>();
        }

        List<PdfAnnotationMessage> replies = [];

        foreach (PdfMarkupAnnotation reply in FindDirectReplies(annotation, repliesByParent))
        {
            replies.AddRange(BuildThread(reply, repliesByParent, processedAnnotations));
        }

        PdfAnnotationMessage? message = annotation.CreateAnnotationMessage(replies.ToArray());

        if (message == null)
        {
            return replies.ToArray();
        }

        return new PdfAnnotationMessage[] { message };
    }

    /// <summary>
    /// Collects the references of every markup annotation on the page.
    /// </summary>
    private static HashSet<PdfReference> BuildMarkupReferences(IReadOnlyList<PdfPageAnnotation> pageAnnotations)
    {
        HashSet<PdfReference> markupReferences = [];

        foreach (PdfPageAnnotation pageAnnotation in pageAnnotations)
        {
            if (pageAnnotation.Content is PdfMarkupAnnotation markupAnnotation && markupAnnotation.Reference != null)
            {
                markupReferences.Add(markupAnnotation.Reference.Value);
            }
        }

        return markupReferences;
    }

    /// <summary>
    /// Groups the page's markup annotations by the annotation they reply to, so that the replies of a given
    /// annotation are found by a single lookup instead of a scan over every annotation on the page.
    /// </summary>
    private static Dictionary<PdfReference, List<PdfMarkupAnnotation>> BuildRepliesByParent(IReadOnlyList<PdfPageAnnotation> pageAnnotations)
    {
        Dictionary<PdfReference, List<PdfMarkupAnnotation>> repliesByParent = [];

        foreach (PdfPageAnnotation pageAnnotation in pageAnnotations)
        {
            if (pageAnnotation.Content is not PdfMarkupAnnotation markupAnnotation
                || markupAnnotation.Reference == null
                || markupAnnotation.InReplyTo == null)
            {
                continue;
            }

            PdfReference parentReference = markupAnnotation.InReplyTo.Value;

            if (!repliesByParent.TryGetValue(parentReference, out List<PdfMarkupAnnotation>? replies))
            {
                replies = new List<PdfMarkupAnnotation>();
                repliesByParent[parentReference] = replies;
            }

            replies.Add(markupAnnotation);
        }

        return repliesByParent;
    }

    private static List<PdfMarkupAnnotation> FindDirectReplies(
        PdfMarkupAnnotation annotation,
        Dictionary<PdfReference, List<PdfMarkupAnnotation>> repliesByParent)
    {
        if (annotation.Reference == null)
        {
            return new List<PdfMarkupAnnotation>();
        }

        if (!repliesByParent.TryGetValue(annotation.Reference.Value, out List<PdfMarkupAnnotation>? replies))
        {
            return new List<PdfMarkupAnnotation>();
        }

        return replies;
    }

    private static bool IsInHoverRectangles(PdfPanelPage page, PdfAnnotationPopup popup, in PdfPoint pagePosition, float scale, float referenceScale)
    {
        foreach (PdfRectangle hoverRectangle in popup.GetHoverRectangles(scale, referenceScale))
        {
            if (page.FromPdfRect(hoverRectangle).Contains(pagePosition))
            {
                return true;
            }
        }

        return false;
    }
}
