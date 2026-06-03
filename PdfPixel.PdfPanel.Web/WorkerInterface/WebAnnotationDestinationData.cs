using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Annotations;
using SkiaSharp;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class WebAnnotationDestinationData
{
    public int PageNumber { get; set; }

    public int FitType { get; set; }

    public float? TargetLeft { get; set; }

    public float? TargetTop { get; set; }

    public float? TargetRight { get; set; }

    public float? TargetBottom { get; set; }

    public float? Zoom { get; set; }

    internal static WebAnnotationDestinationData FromPdfAnnotationDestination(PdfAnnotationDestination destination)
    {
        return new WebAnnotationDestinationData
        {
            PageNumber = destination.PageNumber,
            FitType = (int)destination.FitType,
            TargetLeft = destination.TargetLocation?.Left,
            TargetTop = destination.TargetLocation?.Top,
            TargetRight = destination.TargetLocation?.Right,
            TargetBottom = destination.TargetLocation?.Bottom,
            Zoom = destination.Zoom
        };
    }

    internal PdfAnnotationDestination ToPdfAnnotationDestination()
    {
        SKRect? targetLocation = TargetLeft.HasValue
            ? new SKRect(TargetLeft.Value, TargetTop!.Value, TargetRight!.Value, TargetBottom!.Value)
            : null;

        return new PdfAnnotationDestination
        {
            PageNumber = PageNumber,
            FitType = (PdfDestinationFitType)FitType,
            TargetLocation = targetLocation,
            Zoom = Zoom
        };
    }
}
