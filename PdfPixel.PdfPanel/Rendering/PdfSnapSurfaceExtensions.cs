using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Caching;
using PdfPixel.Shading;
using PdfPixel.Snap;
using PdfPixel.Snap.Rasterization;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Draws the panel's pages onto the Snap surface of a frame.
/// </summary>
internal static class PdfSnapSurfaceExtensions
{
    private const float MaximumArcSegmentDegrees = 90f;

    private static readonly PdfPaint ClipPaint = new(PdfPaintStyle.Clip);
    private static readonly PdfPaint PaperPaint = new(PdfPaintStyle.Fill, new PdfRgbaColor(1f, 1f, 1f, 1f));
    private static readonly PdfPaint ShadowPaint = new PdfPaint(PdfPaintStyle.Fill).WithColorSpace(PdfDeviceRgbColorSpaceConverter.Instance, PdfDeviceColorMapping.None);

    // Thumbnails are DeviceRGB, as the surface they are rendered on.
    private static readonly PdfPaint ThumbnailPaint = new PdfPaint(PdfPaintStyle.Fill).WithColorSpace(PdfDeviceRgbColorSpaceConverter.Instance, PdfDeviceColorMapping.None);

    /// <summary>
    /// Fills the whole surface with the opaque <paramref name="color"/>.
    /// </summary>
    public static void DrawBackground(this PdfSnapSurface surface, in PdfRgbaColor color)
    {
        PdfRgbaColor opaqueColor = new(color.Red, color.Green, color.Blue, 1f);
        PdfPaint backgroundPaint = new(PdfPaintStyle.Fill, opaqueColor);

        surface.DrawPath(CreateRectangle(new PdfRectangle(0, 0, surface.Width, surface.Height)), backgroundPaint);
    }

    /// <summary>
    /// Returns whether the paper of <paramref name="page"/>, its rounded corners left out, covers the whole surface.
    /// </summary>
    public static bool IsCoveredByPage(this PdfSnapSurface surface, in RequestPageInfo page, PagesDrawingRequest request)
    {
        PdfMatrix deviceMatrix = page.GetContentToPanelMatrix(request.Scale);
        PdfRectangle pageBounds = new(0, 0, page.Page.Size.Width, page.Page.Size.Height);
        PdfRectangle pageRect = PdfSnapGrid.SnapToWholeDevicePixels(pageBounds, deviceMatrix);
        float cornerRadius = GetCornerRadius(pageRect, request.PageCornerRadius) * request.Scale;
        PdfRectangle devicePage = deviceMatrix.MapRect(pageRect);
        PdfRectangle surfaceRect = new(0, 0, surface.Width, surface.Height);

        if (!devicePage.Contains(surfaceRect))
        {
            return false;
        }

        bool avoidsLeftCorners = surfaceRect.Left >= devicePage.Left + cornerRadius;
        bool avoidsRightCorners = surfaceRect.Right <= devicePage.Right - cornerRadius;
        bool avoidsTopCorners = surfaceRect.Top >= devicePage.Top + cornerRadius;
        bool avoidsBottomCorners = surfaceRect.Bottom <= devicePage.Bottom - cornerRadius;

        return (avoidsLeftCorners || avoidsTopCorners)
            && (avoidsRightCorners || avoidsTopCorners)
            && (avoidsLeftCorners || avoidsBottomCorners)
            && (avoidsRightCorners || avoidsBottomCorners);
    }

    /// <summary>
    /// Draws <paramref name="page"/> of <paramref name="request"/>: the parts of <paramref name="flags"/>, and with the
    /// content its <paramref name="graphics"/> and <paramref name="drawPageGraphics"/> over them, which draws in the
    /// page's content coordinates.
    /// </summary>
    public static void DrawPage(
        this PdfSnapSurface surface,
        in RequestPageInfo page,
        PagesDrawingRequest request,
        PdfPanelGraphics graphics,
        PageShadow shadow,
        PageDrawFlags flags,
        Action<PdfSnapSurface>? drawPageGraphics)
    {
        ContentLocker<PdfPageTiles> tiles = page.Page.CacheEntry.Tiles;
        PdfMatrix deviceMatrix = page.GetContentToPanelMatrix(request.Scale);
        PdfRectangle pageBounds = new(0, 0, page.Page.Size.Width, page.Page.Size.Height);
        PdfRectangle pageRect = PdfSnapGrid.SnapToWholeDevicePixels(pageBounds, deviceMatrix);
        float cornerRadius = GetCornerRadius(pageRect, request.PageCornerRadius);
        PdfPath pageOutline = CreatePageOutline(pageRect, cornerRadius);

        surface.Save();
        surface.Concat(deviceMatrix);

        if ((flags & PageDrawFlags.Shadow) != 0)
        {
            DrawPageShadow(surface, pageRect, cornerRadius, PageShadow.Extent / request.Scale, shadow);
        }

        surface.DrawPath(pageOutline, ClipPaint);

        if ((flags & PageDrawFlags.Background) != 0)
        {
            surface.DrawPath(pageOutline, PaperPaint);
        }

        bool tilesCoverPage = (flags & PageDrawFlags.Content) != 0 && CoversVisiblePage(surface, tiles, deviceMatrix, pageBounds);

        if ((flags & PageDrawFlags.Thumbnail) != 0 && !tilesCoverPage)
        {
            DrawThumbnail(surface, page.Page, pageRect);
        }

        if ((flags & PageDrawFlags.Content) != 0)
        {
            DrawTiles(surface, tiles, deviceMatrix);

            PdfCommandRecorder? recording = graphics.GetRecording(page.PageNumber);

            if (recording != null)
            {
                surface.Process(new DrawRecordingCommand(recording));
            }

            if (drawPageGraphics != null)
            {
                surface.Save();
                drawPageGraphics(surface);
                surface.Restore();
            }
        }

        surface.Restore();
    }

    /// <summary>
    /// Intersects the clip of <paramref name="surface"/> with the outline of <paramref name="page"/> of
    /// <paramref name="request"/>, the part a partial page draw paints, keeping the current matrix.
    /// </summary>
    public static void ClipToPage(this PdfSnapSurface surface, in RequestPageInfo page, PagesDrawingRequest request)
    {
        PdfMatrix deviceMatrix = page.GetContentToPanelMatrix(request.Scale);
        PdfRectangle pageBounds = new(0, 0, page.Page.Size.Width, page.Page.Size.Height);
        PdfRectangle pageRect = PdfSnapGrid.SnapToWholeDevicePixels(pageBounds, deviceMatrix);
        PdfPath pageOutline = CreatePageOutline(pageRect, GetCornerRadius(pageRect, request.PageCornerRadius));

        surface.Concat(deviceMatrix);
        surface.DrawPath(pageOutline, ClipPaint);
        surface.Concat(deviceMatrix.Invert());
    }

    /// <summary>
    /// Draws <paramref name="shadow"/> around <paramref name="pageRect"/>, rounded by <paramref name="cornerRadius"/>,
    /// fading out at <paramref name="extent"/> from its edge; the page's paper covers its inside.
    /// </summary>
    private static void DrawPageShadow(PdfSnapSurface surface, in PdfRectangle pageRect, float cornerRadius, float extent, PageShadow shadow)
    {
        float left = pageRect.Left;
        float top = pageRect.Top;
        float right = pageRect.Right;
        float bottom = pageRect.Bottom;
        float innerLeft = left + cornerRadius;
        float innerTop = top + cornerRadius;
        float innerRight = right - cornerRadius;
        float innerBottom = bottom - cornerRadius;

        DrawShadowEdge(surface, new PdfRectangle(innerLeft, top - extent, innerRight, top), new PdfPoint(left, top), new PdfPoint(left, top - extent), shadow);
        DrawShadowEdge(surface, new PdfRectangle(innerLeft, bottom, innerRight, bottom + extent), new PdfPoint(left, bottom), new PdfPoint(left, bottom + extent), shadow);
        DrawShadowEdge(surface, new PdfRectangle(left - extent, innerTop, left, innerBottom), new PdfPoint(left, top), new PdfPoint(left - extent, top), shadow);
        DrawShadowEdge(surface, new PdfRectangle(right, innerTop, right + extent, innerBottom), new PdfPoint(right, top), new PdfPoint(right + extent, top), shadow);

        DrawShadowCorner(surface, new PdfRectangle(left - extent, top - extent, innerLeft, innerTop), new PdfPoint(innerLeft, innerTop), cornerRadius, extent, shadow);
        DrawShadowCorner(surface, new PdfRectangle(innerRight, top - extent, right + extent, innerTop), new PdfPoint(innerRight, innerTop), cornerRadius, extent, shadow);
        DrawShadowCorner(surface, new PdfRectangle(left - extent, innerBottom, innerLeft, bottom + extent), new PdfPoint(innerLeft, innerBottom), cornerRadius, extent, shadow);
        DrawShadowCorner(surface, new PdfRectangle(innerRight, innerBottom, right + extent, bottom + extent), new PdfPoint(innerRight, innerBottom), cornerRadius, extent, shadow);
    }

    /// <summary>
    /// Draws the thumbnail samples of <paramref name="page"/> over <paramref name="pageRect"/>, when it has them.
    /// </summary>
    private static void DrawThumbnail(PdfSnapSurface surface, PdfPanelPage page, in PdfRectangle pageRect)
    {
        using LockedContent<PdfPageThumbnail> lockedThumbnail = page.CacheEntry.Thumbnail.GetContent();
        PdfImageSamples? samples = lockedThumbnail.Content?.Samples;

        if (samples == null)
        {
            return;
        }

        surface.DrawImage(samples, PdfMatrix.CreateScaleTranslation(pageRect.Width, pageRect.Height, pageRect.Left, pageRect.Top), ThumbnailPaint, interpolate: true);
    }

    /// <summary>
    /// Returns whether <paramref name="tiles"/> cover the part of the page within <paramref name="pageBounds"/> that
    /// <paramref name="deviceMatrix"/> maps onto <paramref name="surface"/>.
    /// </summary>
    private static bool CoversVisiblePage(PdfSnapSurface surface, ContentLocker<PdfPageTiles> tiles, in PdfMatrix deviceMatrix, in PdfRectangle pageBounds)
    {
        using LockedContent<PdfPageTiles> lockedTiles = tiles.GetContent();

        if (lockedTiles.Content == null)
        {
            return false;
        }

        PdfRectangle surfaceRect = new(0, 0, surface.Width, surface.Height);
        PdfRectangle visibleContent = deviceMatrix.Invert().MapRect(surfaceRect);

        return lockedTiles.Content.Covers(PdfRectangle.Intersect(visibleContent, pageBounds));
    }

    /// <summary>
    /// Draws the cached tiles of a page, whatever scale they were rasterized at, onto <paramref name="surface"/>, whose
    /// current matrix maps page content coordinates to panel pixels.
    /// </summary>
    private static void DrawTiles(PdfSnapSurface surface, ContentLocker<PdfPageTiles> tiles, in PdfMatrix deviceMatrix)
    {
        using LockedContent<PdfPageTiles> lockedTiles = tiles.GetContent();

        if (lockedTiles.Content == null)
        {
            return;
        }

        PdfIntegerRectangle grid = lockedTiles.Content.Grid;

        for (int row = grid.Top; row < grid.Bottom; row++)
        {
            for (int column = grid.Left; column < grid.Right; column++)
            {
                PdfPageTile tile = lockedTiles.Content.GetTile(new PdfIntegerPoint(column, row));

                if (!tile.IsRasterized)
                {
                    continue;
                }

                PdfRectangle destination = PdfSnapGrid.SnapToWholeDevicePixels(tile.Destination, deviceMatrix);

                surface.DrawImage(tile.Decoder, PdfPageTile.TilePaint, PdfMatrix.CreateScaleTranslation(destination.Width, destination.Height, destination.Left, destination.Top));
            }
        }
    }

    /// <summary>
    /// Paints <paramref name="shadow"/> from <paramref name="start"/> on a page edge outward to <paramref name="end"/>,
    /// within <paramref name="region"/>.
    /// </summary>
    private static void DrawShadowEdge(PdfSnapSurface surface, in PdfRectangle region, in PdfPoint start, in PdfPoint end, PageShadow shadow)
    {
        PdfAxialGradient gradient = new(start, end, shadow.Colors, shadow.Positions, extendStart: true, extendEnd: true);

        surface.Save();
        surface.DrawPath(CreateRectangle(region), ClipPaint);
        surface.DrawAxialGradient(gradient, ShadowPaint);
        surface.Restore();
    }

    /// <summary>
    /// Paints <paramref name="shadow"/> around a page corner rounded by <paramref name="radius"/> about
    /// <paramref name="center"/>, fading out at <paramref name="extent"/> beyond it, within <paramref name="region"/>.
    /// </summary>
    private static void DrawShadowCorner(PdfSnapSurface surface, in PdfRectangle region, in PdfPoint center, float radius, float extent, PageShadow shadow)
    {
        PdfRadialGradient gradient = new(center, radius, center, radius + extent, shadow.Colors, shadow.Positions, extendStart: true, extendEnd: true);

        surface.Save();
        surface.DrawPath(CreateRectangle(region), ClipPaint);
        surface.DrawRadialGradient(gradient, ShadowPaint);
        surface.Restore();
    }

    private static PdfPath CreateRectangle(in PdfRectangle rectangle)
    {
        PdfPathBuilder builder = new();
        builder.AddRect(rectangle);

        return builder.ToPath();
    }

    /// <summary>
    /// Returns <paramref name="cornerRadius"/> limited to half the shorter side of <paramref name="rectangle"/>, zero
    /// when it is not positive.
    /// </summary>
    private static float GetCornerRadius(in PdfRectangle rectangle, float cornerRadius)
        => Math.Max(0f, Math.Min(cornerRadius, Math.Min(rectangle.Width, rectangle.Height) / 2f));

    /// <summary>
    /// Returns the outline of <paramref name="rectangle"/> with corners rounded by <paramref name="radius"/>, the
    /// rectangle itself when the radius is zero.
    /// </summary>
    private static PdfPath CreatePageOutline(in PdfRectangle rectangle, float radius)
    {
        PdfPathBuilder outline = new();

        if (radius == 0)
        {
            outline.AddRect(rectangle);
        }
        else
        {
            outline.MoveTo(rectangle.Left + radius, rectangle.Top);
            outline.LineTo(rectangle.Right - radius, rectangle.Top);
            AddArc(outline, new PdfPoint(rectangle.Right - radius, rectangle.Top + radius), radius, 270f, 90f);
            outline.LineTo(rectangle.Right, rectangle.Bottom - radius);
            AddArc(outline, new PdfPoint(rectangle.Right - radius, rectangle.Bottom - radius), radius, 0f, 90f);
            outline.LineTo(rectangle.Left + radius, rectangle.Bottom);
            AddArc(outline, new PdfPoint(rectangle.Left + radius, rectangle.Bottom - radius), radius, 90f, 90f);
            outline.LineTo(rectangle.Left, rectangle.Top + radius);
            AddArc(outline, new PdfPoint(rectangle.Left + radius, rectangle.Top + radius), radius, 180f, 90f);
            outline.Close();
        }

        return outline.ToPath();
    }

    /// <summary>
    /// Adds the circular arc from <paramref name="startDegrees"/> sweeping <paramref name="sweepDegrees"/>, clockwise on
    /// the panel, as cubic segments of at most a quarter turn each, from the current point at the arc's start.
    /// </summary>
    private static void AddArc(PdfPathBuilder builder, in PdfPoint center, float radius, float startDegrees, float sweepDegrees)
    {
        int segmentCount = Math.Max(1, (int)Math.Ceiling(sweepDegrees / MaximumArcSegmentDegrees));
        float segmentRadians = sweepDegrees / segmentCount * MathF.PI / 180f;
        float handle = 4f / 3f * MathF.Tan(segmentRadians / 4f) * radius;
        float angle = startDegrees * MathF.PI / 180f;

        for (int segment = 0; segment < segmentCount; segment++)
        {
            float endAngle = angle + segmentRadians;
            PdfPoint start = new(center.X + (radius * MathF.Cos(angle)), center.Y + (radius * MathF.Sin(angle)));
            PdfPoint end = new(center.X + (radius * MathF.Cos(endAngle)), center.Y + (radius * MathF.Sin(endAngle)));
            PdfPoint control1 = new(start.X - (handle * MathF.Sin(angle)), start.Y + (handle * MathF.Cos(angle)));
            PdfPoint control2 = new(end.X + (handle * MathF.Sin(endAngle)), end.Y - (handle * MathF.Cos(endAngle)));

            builder.CubicTo(control1, control2, end);
            angle = endAngle;
        }
    }

    private static PdfPoint GetArcPoint(in PdfPoint center, float radius, float degrees)
    {
        float radians = degrees * MathF.PI / 180f;

        return new PdfPoint(center.X + (radius * MathF.Cos(radians)), center.Y + (radius * MathF.Sin(radians)));
    }
}
