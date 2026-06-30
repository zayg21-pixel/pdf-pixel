using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System.Linq;

namespace PdfPixel.PdfPanel.Extensions;

internal static class DrawingRequestExtensions
{
    /// <summary>
    /// Computes the visible region of the given page in content coordinates.
    /// Maps the canvas bounds through the inverse content-to-canvas transform,
    /// then clamps to the page bounds.
    /// </summary>
    internal static SKRect ComputeRegionOfInterest(this PagesDrawingRequest request, int pageNumber)
    {
        VisiblePageInfo pageInfo = request.VisiblePages.First(page => page.PageNumber == pageNumber);

        SKMatrix contentToCanvas = pageInfo.GetContentToCanvasMatrix(request.Scale);
        SKRect canvasRect = SKRect.Create(0, 0, request.CanvasSize.Width, request.CanvasSize.Height);
        SKRect regionOfInterest = contentToCanvas.Invert().MapRect(canvasRect);

        SKRect pageBounds = SKRect.Create(0, 0, pageInfo.Info.Width, pageInfo.Info.Height);
        regionOfInterest.Intersect(pageBounds);

        return regionOfInterest;
    }
}
