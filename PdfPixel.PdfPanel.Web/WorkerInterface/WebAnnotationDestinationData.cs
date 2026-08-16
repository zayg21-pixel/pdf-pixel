using PdfPixel.Annotations.Models;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class WebAnnotationDestinationData
{
    public int? PageNumber { get; set; }

    public int FitType { get; set; }

    public WebRect? TargetLocation { get; set; }

    public float? Zoom { get; set; }

    internal static WebAnnotationDestinationData FromPdfDestination(PdfDestination destination)
    {
        return new WebAnnotationDestinationData
        {
            PageNumber = destination.PageNumber,
            FitType = (int)destination.FitType,
            TargetLocation = destination.TargetLocation.HasValue ? WebRect.FromPdfRectangle(destination.TargetLocation.Value) : null,
            Zoom = destination.Zoom
        };
    }

    internal PdfDestination ToPdfDestination()
        => new(PageNumber, (PdfDestinationFitType)FitType, TargetLocation?.ToPdfRectangle(), Zoom);
}
