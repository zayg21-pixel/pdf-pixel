using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Annotations;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class WebAnnotationNavigationData
{
    public int NavigationType { get; set; }

    public int CursorType { get; set; }

    public string Uri { get; set; }

    public WebAnnotationDestinationData Destination { get; set; }

    internal static WebAnnotationNavigationData FromPdfAnnotationNavigation(PdfAnnotationNavigation navigation)
    {
        return new WebAnnotationNavigationData
        {
            NavigationType = (int)navigation.NavigationType,
            CursorType = (int)navigation.CursorType,
            Uri = navigation.Uri,
            Destination = navigation.Destination == null ? null : WebAnnotationDestinationData.FromPdfAnnotationDestination(navigation.Destination)
        };
    }

    internal PdfAnnotationNavigation ToPdfAnnotationNavigation()
    {
        return new PdfAnnotationNavigation
        {
            NavigationType = (PdfAnnotationNavigationType)NavigationType,
            CursorType = (PdfAnnotationCursorType)CursorType,
            Uri = Uri,
            Destination = Destination?.ToPdfAnnotationDestination()
        };
    }
}
