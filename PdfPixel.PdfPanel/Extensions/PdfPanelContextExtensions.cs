using PdfPixel.Annotations.Models;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Settings;
using PdfPixel.PdfPanel.Text;
using System;
using System.Linq;

namespace PdfPixel.PdfPanel.Extensions;

/// <summary>
/// Extension methods for <see cref="PdfPanelContext"/>.
/// </summary>
public static class PdfPanelContextExtensions
{
    /// <summary>
    /// Determines the currently centered page in the panel.
    /// </summary>
    /// <param name="context">The panel context containing pages and panel information.</param>
    /// <returns>The page number of the page whose center is closest to the panel center.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static int GetCurrentPage(this PdfPanelContext context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        int pageCount = context.Pages.Count;
        float panelCenterX = context.HorizontalOffset + (context.PanelWidth / 2f);
        float panelCenterY = context.VerticalOffset + (context.PanelHeight / 2f);
        int closestPageNumber = 1;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < pageCount; i++)
        {
            PdfPanelPage page = context.Pages[i];
            PdfSize rotatedScaledSize = page.GetRotatedScaledSize(context.Scale);

            float pageCenterX = page.Offset.X + (rotatedScaledSize.Width / 2f);
            float pageCenterY = page.Offset.Y + (rotatedScaledSize.Height / 2f);

            float deltaX = pageCenterX - panelCenterX;
            float deltaY = pageCenterY - panelCenterY;
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
    /// Scrolls the panel so the specified page is positioned considering the minimum page gap.
    /// </summary>
    /// <param name="context">The panel context containing pages and panel information.</param>
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
            float pageGap = context.Settings.Layout.PageGap;
            context.VerticalOffset = page.Offset.Y - (pageGap * context.Scale);
            context.HorizontalOffset = page.Offset.X - (pageGap * context.Scale);
        }
    }

    /// <summary>
    /// Increases the current scale by <see cref="PdfPanelZoomSettings.ZoomStep"/> while preserving the panel offset around the provided center.
    /// </summary>
    /// <param name="context">The panel context whose scale will be modified.</param>
    /// <param name="centerX">X coordinate in panel space to preserve while zooming.</param>
    /// <param name="centerY">Y coordinate in panel space to preserve while zooming.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void ZoomIn(this PdfPanelContext context, float centerX, float centerY)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        float scale = context.Scale;
        UpdateScalePreserveOffset(context, scale + (scale * context.Settings.Zoom.ZoomStep), centerX, centerY);
    }

    /// <summary>
    /// Decreases the current scale by <see cref="PdfPanelZoomSettings.ZoomStep"/> while preserving the panel offset around the provided center.
    /// </summary>
    /// <param name="context">The panel context whose scale will be modified.</param>
    /// <param name="centerX">X coordinate in panel space to preserve while zooming.</param>
    /// <param name="centerY">Y coordinate in panel space to preserve while zooming.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static void ZoomOut(this PdfPanelContext context, float centerX, float centerY)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        float scale = context.Scale;
        UpdateScalePreserveOffset(context, scale - (scale * context.Settings.Zoom.ZoomStep), centerX, centerY);

    }

    /// <summary>
    /// Updates the scale of the context and adjusts the horizontal and vertical offsets so the specified
    /// panel center remains focused after the scale change.
    /// </summary>
    /// <param name="context">The panel context to update.</param>
    /// <param name="newScale">The new scale to apply.</param>
    /// <param name="centerX">X coordinate in panel space to preserve while scaling.</param>
    /// <param name="centerY">Y coordinate in panel space to preserve while scaling.</param>
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
    /// Finds the page at the specified panel point.
    /// </summary>
    /// <param name="context">The panel context.</param>
    /// <param name="panelPoint">Point in panel coordinate space.</param>
    /// <returns>The page at the specified point, or <see langword="null"/> if no page is found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    public static PdfPanelPage? GetPageAtPanelPoint(this PdfPanelContext context, in PdfPoint panelPoint)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.Pages == null)
        {
            return null;
        }

        PdfPanelPagePoint? pagePoint = context.ResolvePointerPosition(panelPoint).PagePoint;

        if (pagePoint == null)
        {
            return null;
        }

        context.Pages.TryGetPage(pagePoint.Value.PageNumber, out PdfPanelPage? page);

        return page;
    }

    /// <summary>
    /// Scrolls the panel to the specified annotation destination, optionally updating the zoom level.
    /// </summary>
    /// <param name="context">The panel context to scroll.</param>
    /// <param name="destination">The annotation destination to navigate to.</param>
    public static void ScrollToDestination(this PdfPanelContext context, PdfDestination? destination)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        IPdfPage? destinationPage = destination?.GetPdfPage();

        if (destination == null || destinationPage == null)
        {
            return;
        }

        if (!context.Pages.TryGetPage(destinationPage.PageNumber, out PdfPanelPage? targetPage) || targetPage == null)
        {
            return;
        }

        float? fitZoom = ComputeFitZoom(targetPage, destination, context.PanelWidth, context.PanelHeight);

        if (fitZoom > 0)
        {
            context.Scale = fitZoom.Value;
        }
        else if (destination.Zoom > 0)
        {
            context.Scale = destination.Zoom.Value;
        }

        if (destination.TargetLocation.HasValue)
        {
            PdfRectangle pdfRect = destination.TargetLocation.Value;
            PdfPoint pdfLocation = new(pdfRect.Left, pdfRect.Top);
            PdfPoint pageLocation = targetPage.FromPdfPoint(pdfLocation);

            PdfMatrix pageToExtent = targetPage.PanelToPageMatrix(context.Scale, 0, 0).Invert();
            PdfPoint extentLocation = pageToExtent.MapPoint(pageLocation);

            context.HorizontalOffset = extentLocation.X;
            context.VerticalOffset = extentLocation.Y;
        }
        else
        {
            context.ScrollToPage(targetPage.PageNumber);
        }
    }

    /// <summary>
    /// Scrolls the panel so the specified search match is centered in it.
    /// </summary>
    /// <param name="context">The panel context to scroll.</param>
    /// <param name="match">The search match to navigate to.</param>
    public static void ScrollToSearchMatch(this PdfPanelContext context, in PdfPanelSearchMatch match)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (!context.Pages.TryGetPage(match.Range.PageNumber, out PdfPanelPage? targetPage) || targetPage == null)
        {
            return;
        }

        PdfMatrix pageToExtent = targetPage.PanelToPageMatrix(context.Scale, 0, 0).Invert();
        PdfPoint extentCenter = pageToExtent.MapPoint(new PdfPoint(match.Bounds.MidX, match.Bounds.MidY));

        context.HorizontalOffset = extentCenter.X - (context.PanelWidth / 2);
        context.VerticalOffset = extentCenter.Y - (context.PanelHeight / 2);
    }

    private static float? ComputeFitZoom(PdfPanelPage page, PdfDestination destination, float panelWidth, float panelHeight)
    {
        PdfSize pageSize = page.GetRotatedSize();

        return destination.FitType switch
        {
            PdfDestinationFitType.Fit or PdfDestinationFitType.FitB =>
                Math.Min(panelWidth / pageSize.Width, panelHeight / pageSize.Height),
            PdfDestinationFitType.FitH or PdfDestinationFitType.FitBH =>
                panelWidth / pageSize.Width,
            PdfDestinationFitType.FitV or PdfDestinationFitType.FitBV =>
                panelHeight / pageSize.Height,
            PdfDestinationFitType.FitR when destination.TargetLocation is { Width: > 0, Height: > 0 } rect =>
                Math.Min(panelWidth / rect.Width, panelHeight / rect.Height),
            _ => null
        };
    }
}
