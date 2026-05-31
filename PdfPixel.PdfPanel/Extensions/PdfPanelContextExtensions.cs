using PdfPixel.Annotations.Models;
using PdfPixel.PdfPanel.Annotations;
using SkiaSharp;
using System;
using System.Linq;

namespace PdfPixel.PdfPanel.Extensions;

/// <summary>
/// Extension methods for <see cref="PdfPanelContext"/>.
/// </summary>
public static class PdfPanelContextExtensions
{
    private const float ScaleTolerance = 0.001f;

    /// <summary>
    /// Determines the currently centered page in the viewport.
    /// </summary>
    /// <param name="context">The panel context containing pages and viewport information.</param>
    /// <returns>The page number of the page whose center is closest to the viewport center.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static int GetCurrentPage(this PdfPanelContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        int pageCount = context.Pages.Count;
        float viewportCenterX = context.HorizontalOffset + (context.ViewportWidth / 2f);
        float viewportCenterY = context.VerticalOffset + (context.ViewportHeight / 2f);
        int closestPageNumber = 1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = context.Pages[i];
            SKSize rotatedScaledSize = page.GetRotatedScaledSize(context.Scale);

            float pageCenterX = page.Offset.X + (rotatedScaledSize.Width / 2f);
            float pageCenterY = page.Offset.Y + (rotatedScaledSize.Height / 2f);

            float deltaX = pageCenterX - viewportCenterX;
            float deltaY = pageCenterY - viewportCenterY;
            float distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);

            if (distanceSquared < closestDistance)
            {
                closestDistance = distanceSquared;
                closestPageNumber = page.PageNumber;
            }
        }

        return closestPageNumber;
    }

    /// <summary>
    /// Scrolls the viewport so the specified page is positioned considering the minimum page gap.
    /// </summary>
    /// <param name="context">The panel context containing pages and viewport information.</param>
    /// <param name="pageNumber">The page number to scroll to.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void ScrollToPage(this PdfPanelContext context, int pageNumber)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        PdfPanelPage? page = context.Pages.FirstOrDefault(p => p.PageNumber == pageNumber);
        if (page != null)
        {
            context.VerticalOffset = page.Offset.Y - (context.MinimumPageGap * context.Scale);
            context.HorizontalOffset = page.Offset.X - (context.MinimumPageGap * context.Scale);
        }
    }

    /// <summary>
    /// Increases the current scale by the specified factor while preserving the viewport offset around the provided center.
    /// </summary>
    /// <param name="context">The panel context whose scale will be modified.</param>
    /// <param name="factor">The proportional factor to increase the scale by (e.g. 0.1 for +10%).</param>
    /// <param name="centerX">X coordinate in viewport space to preserve while zooming.</param>
    /// <param name="centerY">Y coordinate in viewport space to preserve while zooming.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void ZoomIn(this PdfPanelContext context, float factor, float centerX, float centerY)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        float scale = context.Scale;
        UpdateScalePreserveOffset(context, scale + (scale * factor), centerX, centerY);
    }

    /// <summary>
    /// Decreases the current scale by the specified factor while preserving the viewport offset around the provided center.
    /// </summary>
    /// <param name="context">The panel context whose scale will be modified.</param>
    /// <param name="factor">The proportional factor to decrease the scale by (e.g. 0.1 for -10%).</param>
    /// <param name="centerX">X coordinate in viewport space to preserve while zooming.</param>
    /// <param name="centerY">Y coordinate in viewport space to preserve while zooming.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void ZoomOut(this PdfPanelContext context, float factor, float centerX, float centerY)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        float scale = context.Scale;
        UpdateScalePreserveOffset(context, scale - (scale * factor), centerX, centerY);

    }

    /// <summary>
    /// Updates the scale of the context and adjusts the horizontal and vertical offsets so the specified
    /// viewport center remains focused after the scale change.
    /// </summary>
    /// <param name="context">The panel context to update.</param>
    /// <param name="newScale">The new scale to apply.</param>
    /// <param name="centerX">X coordinate in viewport space to preserve while scaling.</param>
    /// <param name="centerY">Y coordinate in viewport space to preserve while scaling.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void UpdateScalePreserveOffset(this PdfPanelContext context, float newScale, float centerX, float centerY)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        float oldScale = context.Scale;

        context.VerticalOffset =
            ((context.VerticalOffset + centerY) * (newScale / oldScale)) - centerY;

        context.HorizontalOffset =
            ((context.HorizontalOffset + centerX) * (newScale / oldScale)) - centerX;

        context.Scale = newScale;
    }

    /// <summary>
    /// Sets the automatic scaling mode for the panel and applies scaling to pages depending on the selected mode.
    /// </summary>
    /// <param name="context">The panel context whose auto scale mode will be applied.</param>
    /// <param name="mode">The auto scale mode to apply.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void SetAutoScaleMode(this PdfPanelContext context, PdfPanelAutoScaleMode mode)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        switch (mode)
        {
            case PdfPanelAutoScaleMode.NoAutoScale:
            {
                break;
            }
            case PdfPanelAutoScaleMode.ScaleToWidth:
            {
                ApplyScaleToPages(context, context.Pages);
                break;
            }
            case PdfPanelAutoScaleMode.ScaleToHeight:
            {
                ApplyScaleToPagesHeight(context, context.Pages);
                break;
            }
        }
    }

    private static void ApplyScaleToPages(PdfPanelContext context, PdfPanelPageCollection pages)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (pages == null || pages.Count == 0)
        {
            return;
        }

        float maxPageWidth = 0;

        foreach (PdfPanelPage page in pages)
        {
            maxPageWidth = Math.Max(maxPageWidth, page.GetRotatedSize().Width);
        }

        float padding = context.PagesPadding.Left + context.PagesPadding.Right;
        float scale = MathF.Max(context.MinScale, MathF.Min(context.MaxScale, (context.ViewportWidth - padding) / maxPageWidth));

        if (Math.Abs(scale - context.Scale) / context.Scale <= ScaleTolerance)
        {
            return;
        }

        UpdateScalePreserveOffset(context, scale, 0, 0);
    }

    private static void ApplyScaleToPagesHeight(PdfPanelContext context, PdfPanelPageCollection pages)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (pages == null || pages.Count == 0)
        {
            return;
        }

        float maxPageHeight = 0;

        foreach (PdfPanelPage page in pages)
        {
            maxPageHeight = Math.Max(maxPageHeight, page.GetRotatedSize().Height);
        }

        float padding = context.MinimumPageGap;
        float scale = MathF.Max(context.MinScale, MathF.Min(context.MaxScale, (context.ViewportHeight - padding) / maxPageHeight));

        if (Math.Abs(scale - context.Scale) / context.Scale <= ScaleTolerance)
        {
            return;
        }

        UpdateScalePreserveOffset(context, scale, 0, 0);
    }


    /// <summary>
    /// Finds the page at the specified viewport point.
    /// </summary>
    /// <param name="context">The panel context.</param>
    /// <param name="viewportPoint">Point in viewport coordinate space.</param>
    /// <returns>The page at the specified point, or <see langword="null"/> if no page is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static PdfPanelPage? GetPageAtViewportPoint(this PdfPanelContext context, SKPoint viewportPoint)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Pages == null)
        {
            return null;
        }

        foreach (PdfPanelPage page in context.Pages)
        {
            if (!page.IsPageVisible(context.ViewportRectangle, context.Scale))
            {
                continue;
            }

            SKMatrix matrix = page.ViewportToPageMatrix(context);
            SKPoint testPagePoint = matrix.MapPoint(viewportPoint);

            if (page.IsPointInPageBounds(testPagePoint))
            {
                return page;
            }
        }

        return null;
    }

    /// <summary>
    /// Scrolls the viewport to the specified PDF destination, optionally updating the zoom level.
    /// </summary>
    /// <param name="context">The panel context to scroll.</param>
    /// <param name="destination">The PDF destination to navigate to.</param>
    public static void ScrollToDestination(this PdfPanelContext context, PdfDestination destination)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (destination == null)
        {
            return;
        }

        Models.IPdfPage? destinationPage = destination.GetPdfPage();
        if (destinationPage == null)
        {
            return;
        }

        if (!context.Pages.TryGetPage(destinationPage.PageNumber, out PdfPanelPage? targetPage) || targetPage == null)
        {
            return;
        }

        if (destination.Zoom > 0)
        {
            context.Scale = destination.Zoom.Value;
        }

        SKRect? targetLocation = destination.GetTargetLocation();
        if (targetLocation.HasValue)
        {
            SKRect pdfRect = targetLocation.Value;
            SKPoint pdfLocation = new(pdfRect.Left, pdfRect.Top);
            SKPoint pageLocation = targetPage.FromPdfPoint(pdfLocation);

            SKMatrix pageToCanvas = targetPage.ViewportToPageMatrix(context.Scale, 0, 0).Invert();
            SKPoint canvasLocation = pageToCanvas.MapPoint(pageLocation);

            context.HorizontalOffset = canvasLocation.X;
            context.VerticalOffset = canvasLocation.Y;
        }
        else
        {
            context.ScrollToPage(targetPage.PageNumber);
        }
    }
}
