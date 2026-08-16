using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.ContentProvider;

internal static class PdfDocumentAnnotationExtractor
{
    public static PdfAnnotationPopup? GetActiveAnnotation(PdfPanelPage page, SKPoint pagePosition)
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
        // TODO: rework
        return Array.Empty<PdfAnnotationPopup>();
        if (pageNumber < 1 || pageNumber > document.Pages.Count)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        IPdfPage pdfPage = document.Pages[pageNumber - 1];
        if (pdfPage.Annotations.Count == 0)
        {
            return Array.Empty<PdfAnnotationPopup>();
        }

        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent = BuildRepliesByParent(pdfPage);
        List<PdfAnnotationPopup> popups = [];
        HashSet<PdfAnnotationBase> processedAnnotations = [];

        foreach (PdfPageAnnotation pageAnnotation in pdfPage.Annotations)
        {
            PdfAnnotationBase annotation = pageAnnotation.Content;

            if (processedAnnotations.Contains(annotation))
            {
                continue;
            }

            if (annotation.InReplyTo.HasValue)
            {
                continue;
            }

            PdfAnnotationMessage[] thread = BuildAnnotationThread(annotation, repliesByParent, processedAnnotations);
            PdfAnnotationNavigation? navigation = BuildAnnotationNavigation(annotation);
            popups.Add(new PdfAnnotationPopup(pageAnnotation, navigation, thread));
        }

        return popups.ToArray();
    }

    private static PdfAnnotationMessage[] BuildAnnotationThread(
        PdfAnnotationBase rootAnnotation,
        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        List<PdfAnnotationMessage> messages = [];

        PdfAnnotationMessage? rootMessage = CreateAnnotationMessage(rootAnnotation);
        if (rootMessage.HasValue)
        {
            messages.Add(rootMessage.Value);
        }

        processedAnnotations.Add(rootAnnotation);

        List<PdfAnnotationBase> replies = FindAllReplies(rootAnnotation, repliesByParent, processedAnnotations);

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
    private static PdfAnnotationMessage? CreateAnnotationMessage(PdfAnnotationBase annotation)
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

        return new PdfAnnotationMessage(messageDate, messageTitle, contents);
    }

    private static List<PdfAnnotationBase> FindAllReplies(
        PdfAnnotationBase annotation,
        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        List<PdfAnnotationBase> replies = [];
        PdfReference annotationRef = annotation.Reference;

        if (!annotationRef.IsValid)
        {
            return replies;
        }

        List<PdfAnnotationBase> directReplies = FindDirectReplies(annotationRef, repliesByParent, processedAnnotations);

        foreach (PdfAnnotationBase reply in directReplies)
        {
            replies.Add(reply);

            if (reply.ReplyType == PdfAnnotationReplyType.Reply)
            {
                List<PdfAnnotationBase> nestedReplies = FindAllReplies(reply, repliesByParent, processedAnnotations);
                replies.AddRange(nestedReplies);
            }
        }

        return replies;
    }

    private static PdfAnnotationNavigation BuildAnnotationNavigation(PdfAnnotationBase annotation)
    {
        if (annotation is not PdfLinkAnnotation link)
        {
            return new PdfAnnotationNavigation
            {
                NavigationType = PdfAnnotationNavigationType.None,
                CursorType = annotation.CursorType
            };
        }

        if (link.Action is PdfUriAction uriAction && uriAction.Uri != null)
        {
            return new PdfAnnotationNavigation
            {
                NavigationType = PdfAnnotationNavigationType.Uri,
                CursorType = link.CursorType,
                Uri = uriAction.Uri.Value.ToString()
            };
        }

        if (link.Action is PdfGoToAction goToAction)
        {
            PdfDestination? actionDestination = goToAction.GetDestination();

            if (actionDestination != null)
            {
                return new PdfAnnotationNavigation
                {
                    NavigationType = PdfAnnotationNavigationType.GoToDestination,
                    CursorType = link.CursorType,
                    Destination = actionDestination
                };
            }
        }

        if (link.Action is PdfGoToRemoteAction)
        {
            // TODO: handle remote file loading
            return new PdfAnnotationNavigation
            {
                NavigationType = PdfAnnotationNavigationType.GoToRemote,
                CursorType = link.CursorType
            };
        }

        PdfDestination? linkDestination = link.GetDestination();

        if (linkDestination != null)
        {
            return new PdfAnnotationNavigation
            {
                NavigationType = PdfAnnotationNavigationType.GoToDestination,
                CursorType = link.CursorType,
                Destination = linkDestination
            };
        }

        return new PdfAnnotationNavigation
        {
            NavigationType = PdfAnnotationNavigationType.None,
            CursorType = link.CursorType
        };
    }

    private static List<PdfAnnotationBase> FindDirectReplies(
        in PdfReference parentRef,
        Dictionary<PdfReference, List<PdfAnnotationBase>> repliesByParent,
        HashSet<PdfAnnotationBase> processedAnnotations)
    {
        List<PdfAnnotationBase> replies = [];

        if (!repliesByParent.TryGetValue(parentRef, out List<PdfAnnotationBase>? candidates))
        {
            return replies;
        }

        foreach (PdfAnnotationBase candidate in candidates)
        {
            if (processedAnnotations.Contains(candidate))
            {
                continue;
            }

            processedAnnotations.Add(candidate);
            replies.Add(candidate);
        }

        return replies;
    }
}
