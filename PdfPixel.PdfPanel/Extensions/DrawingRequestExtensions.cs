using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Requests;
using System.Linq;

namespace PdfPixel.PdfPanel.Extensions;

internal static class DrawingRequestExtensions
{
    /// <summary>
    /// Computes the visible region of the given page in content coordinates.
    /// Maps the canvas bounds through the inverse content-to-canvas transform,
    /// then clamps to the page bounds.
    /// </summary>
    internal static PdfRectangle ComputeRegionOfInterest(this PagesDrawingRequest request, int pageNumber)
    {
        VisiblePageInfo pageInfo = request.VisiblePages.First(page => page.PageNumber == pageNumber);

        PdfMatrix contentToCanvas = pageInfo.GetContentToCanvasMatrix(request.Scale);
        PdfRectangle canvasRect = PdfRectangle.FromLocationAndSize(0, 0, request.CanvasSize.Width, request.CanvasSize.Height);
        PdfRectangle regionOfInterest = contentToCanvas.Invert().MapRect(canvasRect);

        PdfRectangle pageBounds = PdfRectangle.FromLocationAndSize(0, 0, pageInfo.Info.Width, pageInfo.Info.Height);

        return PdfRectangle.Intersect(regionOfInterest, pageBounds);
    }
}
