using SkiaSharp;
using System.Linq;

namespace PdfPixel.PdfPanel.Extensions;

public static class PdfPanelContextExtensions
{
    public static int GetCurrentPage(this PdfPanelContext context)
    {
        int pageCount = context.Pages.Count;
        float viewportCenterX = context.HorizontalOffset + context.ViewportWidth / 2f;
        float viewportCenterY = context.VerticalOffset + context.ViewportHeight / 2f;
        int closestPageNumber = 1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = context.Pages[i];
            SKSize rotatedScaledSize = page.GetRotatedScaledSize(context.Scale);

            float pageCenterX = page.Offset.X + rotatedScaledSize.Width / 2f;
            float pageCenterY = page.Offset.Y + rotatedScaledSize.Height / 2f;

            float deltaX = pageCenterX - viewportCenterX;
            float deltaY = pageCenterY - viewportCenterY;
            float distanceSquared = deltaX * deltaX + deltaY * deltaY;

            if (distanceSquared < closestDistance)
            {
                closestDistance = distanceSquared;
                closestPageNumber = page.PageNumber;
            }
        }

        return closestPageNumber;
    }

    public static void ScrollToPage(this PdfPanelContext context, int pageNumber)
    {
        var page = context.Pages.FirstOrDefault(p => p.PageNumber == pageNumber);
        if (page != null)
        {
            context.VerticalOffset = page.Offset.Y - context.MinimumPageGap * context.Scale;
            context.HorizontalOffset = page.Offset.X - context.MinimumPageGap * context.Scale;
        }
    }

    public static void ZoomIn(this PdfPanelContext context, float factor, float centerX, float centerY)
    {
        var scale = context.Scale;
        UpdateScalePreserveOffset(context, scale + scale * factor, centerX, centerY);
    }

    public static void ZoomOut(this PdfPanelContext context, float factor, float centerX, float centerY)
    {
        var scale = context.Scale;
        UpdateScalePreserveOffset(context, scale - scale * factor, centerX, centerY);

    }

    public static void UpdateScalePreserveOffset(this PdfPanelContext context, float newScale, float centerX, float centerY)
    {
        var oldScale = context.Scale;

        context.VerticalOffset =
            (context.VerticalOffset + centerY) * (newScale / oldScale) - centerY;

        context.HorizontalOffset =
            (context.HorizontalOffset + centerX) * (newScale / oldScale) - centerX;

        context.Scale = newScale;
    }

    public static void SetAutoScaleMode(this PdfPanelContext context, PdfPanelAutoScaleMode mode)
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
                    var maxVisibleWidth = context.Pages.Max(x => x.Info.GetRotatedSize(x.UserRotation).Width) + context.PagesPadding.Left + context.PagesPadding.Right + 1;
                    var scale = context.ViewportWidth / maxVisibleWidth;
                    UpdateScalePreserveOffset(context, scale, 0, 0);
                    break;
                }
            case PdfPanelAutoScaleMode.ScaleToVisible:
                {
                    var visiblePages = context.Pages.Where(p => p.IsPageVisible(context.ViewportRectangle, context.Scale)).ToList();
                    var maxVisibleWidth = visiblePages.Max(x => x.Info.GetRotatedSize(x.UserRotation).Width) + context.PagesPadding.Left + context.PagesPadding.Right + 1;
                    var scale = context.ViewportWidth / maxVisibleWidth;

                    UpdateScalePreserveOffset(context, scale, 0, 0);
                    break;
                }
        }
    }
}
