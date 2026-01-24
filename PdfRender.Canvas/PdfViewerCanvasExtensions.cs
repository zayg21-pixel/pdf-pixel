using System.Linq;

namespace PdfRender.Canvas;

public enum PdfPanelAutoScaleMode
{
    /// <summary>
    /// No automatic scaling.
    /// </summary>
    NoAutoScale,

    /// <summary>
    /// Scale to visible pages.
    /// </summary>
    ScaleToVisible,

    /// <summary>
    /// Scale to all pages.
    /// </summary>
    ScaleToWidth
}

public static class PdfViewerCanvasExtensions
{
    public static void ScrollToPage(this PdfViewerCanvas canvas, int pageNumber)
    {
        var page = canvas.Pages.FirstOrDefault(p => p.PageNumber == pageNumber);
        if (page != null)
        {
            canvas.VerticalOffset = page.Offset.Y - canvas.MinimumPageGap * canvas.Scale;
            canvas.HorizontalOffset = page.Offset.X - canvas.MinimumPageGap * canvas.Scale;
        }
    }

    public static void ZoomIn(this PdfViewerCanvas canvas, float factor, float centerX, float centerY)
    {
        var scale = canvas.Scale;
        UpdateScalePreserveOffset(canvas, scale + scale * factor, centerX, centerY);
    }

    public static void ZoomOut(this PdfViewerCanvas canvas, float factor, float centerX, float centerY)
    {
        var scale = canvas.Scale;
        UpdateScalePreserveOffset(canvas, scale - scale * factor, centerX, centerY);

    }

    public static void UpdateScalePreserveOffset(this PdfViewerCanvas canvas, float newScale, float centerX, float centerY)
    {
        var oldScale = canvas.Scale;

        canvas.VerticalOffset =
            (canvas.VerticalOffset + centerY) * (newScale / oldScale) - centerY;

        canvas.HorizontalOffset =
            (canvas.HorizontalOffset + centerX) * (newScale / oldScale) - centerX;

        canvas.Scale = newScale;
    }

    public static void SetAutoScaleMode(this PdfViewerCanvas canvas, PdfPanelAutoScaleMode mode)
    {
        switch (mode)
        {
            case PdfPanelAutoScaleMode.NoAutoScale:
                {
                    break;
                }
            case PdfPanelAutoScaleMode.ScaleToWidth:
                {
                    // TODO: implement correctly depending on layout
                    var maxVisibleWidth = canvas.Pages.Max(x => x.Info.GetRotatedSize(x.UserRotation).Width) + canvas.PagesPadding.Left + canvas.PagesPadding.Right + 1;
                    var scale = canvas.Width / maxVisibleWidth;
                    UpdateScalePreserveOffset(canvas, scale, 0, 0);
                    break;
                }
            case PdfPanelAutoScaleMode.ScaleToVisible:
                {
                    var visiblePages = canvas.Pages.Where(p => canvas.IsPageVisible(p)).ToList();
                    var maxVisibleWidth = visiblePages.Max(x => x.Info.GetRotatedSize(x.UserRotation).Width) + canvas.PagesPadding.Left + canvas.PagesPadding.Right + 1;
                    var scale = canvas.Width / maxVisibleWidth;

                    UpdateScalePreserveOffset(canvas, scale, 0, 0);
                    break;
                }
        }
    }
}
