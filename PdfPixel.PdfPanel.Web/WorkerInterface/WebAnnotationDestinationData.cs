using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Annotations;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class WebAnnotationDestinationData
{
    public int PageNumber { get; set; }

    public int FitType { get; set; }

    public WebRect? TargetLocation { get; set; }

    public float? Zoom { get; set; }

    internal static WebAnnotationDestinationData FromPdfAnnotationDestination(PdfAnnotationDestination destination)
    {
        return new WebAnnotationDestinationData
        {
            PageNumber = destination.PageNumber,
            FitType = (int)destination.FitType,
            TargetLocation = destination.TargetLocation.HasValue ? WebRect.FromSkRect(destination.TargetLocation.Value) : null,
            Zoom = destination.Zoom
        };
    }

    internal PdfAnnotationDestination ToPdfAnnotationDestination()
    {
        return new PdfAnnotationDestination
        {
            PageNumber = PageNumber,
            FitType = (PdfDestinationFitType)FitType,
            TargetLocation = TargetLocation?.ToSkRect(),
            Zoom = Zoom
        };
    }
}
