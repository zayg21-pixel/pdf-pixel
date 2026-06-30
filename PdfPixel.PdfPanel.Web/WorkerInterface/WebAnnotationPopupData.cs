using PdfPixel.PdfPanel.Annotations;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class WebAnnotationPopupData
{
    public int PageNumber { get; set; }

    public bool IsInteractive { get; set; }

    public WebRect HoverRectangle { get; set; }

    public WebAnnotationNavigationData Navigation { get; set; }

    public List<WebAnnotationMessageData> Messages { get; set; }

    internal static WebAnnotationPopupData FromPdfAnnotationPopup(PdfAnnotationPopup popup, int pageNumber)
    {
        return new WebAnnotationPopupData
        {
            PageNumber = pageNumber,
            IsInteractive = popup.IsInteractive,
            HoverRectangle = WebRect.FromSkRect(popup.HoverRectangle),
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
        PdfAnnotationNavigation navigation = Navigation?.ToPdfAnnotationNavigation();

        PdfAnnotationMessage[] messages = Messages == null
            ? System.Array.Empty<PdfAnnotationMessage>()
            : Messages.Select(m => new PdfAnnotationMessage(
                m.CreationDate == null ? null : (System.DateTimeOffset?)System.DateTimeOffset.Parse(m.CreationDate),
                m.Title,
                m.Contents)).ToArray();

        return new PdfAnnotationPopup(navigation, IsInteractive, HoverRectangle.ToSkRect(), messages);
    }
}
