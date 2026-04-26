using PdfPixel.Annotations.Models;
using SkiaSharp;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Document-independent representation of a resolved annotation action.
/// All values are eagerly resolved at annotation extraction time.
/// </summary>
public class PdfPanelAnnotationAction
{
    public PdfActionType ActionType { get; set; }

    /// <summary>Target name string for actions.</summary>
    public string TargetName { get; set; }

    /// <summary>1-based destination page number for <see cref="PdfActionType.GoTo"/> actions.</summary>
    public int? PageNumber { get; set; }

    /// <summary>Destination rectangle in PDF coordinates for <see cref="PdfActionType.GoTo"/> actions.</summary>
    public SKRect? TargetRect { get; set; }

    /// <summary>Destination zoom level for <see cref="PdfActionType.GoTo"/> actions.</summary>
    public float? Zoom { get; set; }


    public static PdfPanelAnnotationAction FromLinkAnnotation(PdfLinkAnnotation link)
    {
        if (link == null)
        {
            return null;
        }
        
        if (link.Action is PdfUriAction uriAction && !uriAction.Uri.IsEmpty)
        {
            var uri = uriAction.Uri.ToString();
            return new PdfPanelAnnotationAction
            {
                ActionType = PdfActionType.Uri,
                TargetName = uri
            };
        }
        else if (link.Action is PdfGoToAction goToAction && goToAction.Destination != null)
        {
            return ResolveGoToDestination(goToAction.Destination);
        }
        else if (link.Action is PdfGoToRemoteAction goToRemote && !goToRemote.FileSpecification.IsEmpty)
        {
            return new PdfPanelAnnotationAction
            {
                ActionType = PdfActionType.GoToRemote,
                TargetName = goToRemote.FileSpecification.ToString()
            };
        }
        else if (link.Destination != null)
        {
            return ResolveGoToDestination(link.Destination);
        }
        else
        {
            return null;
        }
    }

    private static PdfPanelAnnotationAction ResolveGoToDestination(PdfDestination destination)
    {
        var page = destination.GetPdfPage();

        return new PdfPanelAnnotationAction
        {
            ActionType = PdfActionType.GoTo,
            PageNumber = page?.PageNumber,
            TargetRect = destination.GetTargetLocation(),
            Zoom = destination.Zoom
        };
    }
}
