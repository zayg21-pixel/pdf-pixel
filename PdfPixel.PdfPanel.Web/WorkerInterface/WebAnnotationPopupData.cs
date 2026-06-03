using PdfPixel.PdfPanel.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class WebAnnotationPopupData
{
    public int PageNumber { get; set; }

    public bool IsInteractive { get; set; }

    public float HoverLeft { get; set; }

    public float HoverTop { get; set; }

    public float HoverRight { get; set; }

    public float HoverBottom { get; set; }

    public WebAnnotationNavigationData? Navigation { get; set; }

    public List<WebAnnotationMessageData> Messages { get; set; }

    internal static WebAnnotationPopupData FromPdfAnnotationPopup(PdfAnnotationPopup popup, int pageNumber)
    {
        var hover = popup.HoverRectangle;

        return new WebAnnotationPopupData
        {
            PageNumber = pageNumber,
            IsInteractive = popup.IsInteractive,
            HoverLeft = hover.Left,
            HoverTop = hover.Top,
            HoverRight = hover.Right,
            HoverBottom = hover.Bottom,
            Navigation = popup.Navigation == null ? null : WebAnnotationNavigationData.FromPdfAnnotationNavigation(popup.Navigation),
            Messages = popup.Messages.Select(m => new WebAnnotationMessageData
            {
                Title = m.Title,
                Contents = m.Contents,
                CreationDate = m.CreationDate?.ToString("O")
            }).ToList()
        };
    }

    internal PdfAnnotationPopup ToPdfAnnotationPopup()
    {
        var hover = new SkiaSharp.SKRect(HoverLeft, HoverTop, HoverRight, HoverBottom);

        PdfAnnotationNavigation? navigation = Navigation?.ToPdfAnnotationNavigation();

        PdfAnnotationMessage[] messages = Messages == null
            ? System.Array.Empty<PdfAnnotationMessage>()
            : Messages.Select(m => new PdfAnnotationMessage(
                m.CreationDate == null ? null : (System.DateTimeOffset?)System.DateTimeOffset.Parse(m.CreationDate),
                m.Title,
                m.Contents)).ToArray();

        return new PdfAnnotationPopup(navigation, IsInteractive, hover, messages);
    }
}
