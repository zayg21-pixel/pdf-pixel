using PdfRender.Canvas.Requests;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace PdfRender.Canvas;

public enum PageRenderTiling
{
    Vertical,
    TwoColumns
}

public class PdfViewerCanvas
{
    private readonly PdfRenderingQueue _pdfRenderingQueue;
    private ICanvasRenderTargetFactory _renderTargetFactory;

    public PdfViewerCanvas(PdfViewerPageCollection pages, PdfRenderingQueue renderingQueue, ICanvasRenderTargetFactory renderTargetFactory)
    {
        Pages = pages;
        _pdfRenderingQueue = renderingQueue;
        _renderTargetFactory = renderTargetFactory;
    }

    /// <summary>
    /// Width of the viewing area.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Height of the viewing area.
    /// </summary>
    public float Height { get; set; }

    /// <summary>
    /// Total width of all pages including padding.
    /// </summary>
    public float ExtentWidth { get; private set; }

    /// <summary>
    /// Total height of all pages including padding.
    /// </summary>
    public float ExtentHeight { get; private set; }

    /// <summary>
    /// Gets or sets the index of the current page in a paginated collection.
    /// </summary>
    public int CurrentPage { get; set; }

    public float VerticalOffset { get; set; }

    public float HorizontalOffset { get; set; }

    public float Scale { get; set; }

    /// <summary>
    /// Padding from the edges of the viewing area to the pages.
    /// </summary>
    public SKRect PagesPadding { get; set; }

    /// <summary>
    /// Gets or sets the spacing, between pages in the layout.
    /// </summary>
    public float MinimumPageGap { get; set; }

    public SKColor BackgroundColor { get; set; }

    public int MaxThumbnailSize { get; set; }

    public PdfViewerPageCollection Pages { get; }

    /// <summary>
    /// Returns collection of pages that are visible on panel.
    /// </summary>
    /// <returns>Visible pages.</returns>
    //public IEnumerable<PdfViewerPage> GetVisiblePages()
    //{
    //    for (int i = 0; i < Pages.Count; i++)
    //    {
    //        var page = Pages[i];
    //        if (IsPageVisible(page))
    //        {
    //            yield return page;
    //        }
    //    }
    //}
    private IEnumerable<VisiblePageInfo> GetVisiblePages()
    {
        if (Pages == null)
        {
            yield break;
        }

        float verticalOffset = -VerticalOffset / Scale + PagesPadding.Top;
        float horizontalOffset = -HorizontalOffset / Scale;

        var centerOffset = GetCenterOffset() / Scale;
        horizontalOffset += centerOffset;

        for (int i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            var rotatedSize = page.Info.GetRotatedSize(page.UserRotation);

            if (page.IsVisible(verticalOffset, Height / Scale))
            {
                var pageOffsetLeft = (ExtentWidth / Scale - rotatedSize.Width) / 2;
                var offset = new SKPoint(horizontalOffset + pageOffsetLeft, verticalOffset);
                yield return new VisiblePageInfo(i + 1, offset, page.Info, page.UserRotation);
            }

            verticalOffset += rotatedSize.Height + MinimumPageGap;
        }
    }

    public float GetCenterOffset()
    {
        var centerOffset = (Width - ExtentWidth) / 2;

        if (centerOffset < 0)
        {
            centerOffset = 0;
        }

        return centerOffset;
    }

    public bool IsPageVisible(PdfViewerPage page)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        var pageHeight = page.Info.GetRotatedSize(page.UserRotation).Height;
        float canvasHeight = Height / Scale;
        var pageTop = page.Offset.X;
        var pageBottom = page.Offset.X + pageHeight;

        return (pageTop >= 0 && pageTop <= canvasHeight) || (pageBottom >= 0 && pageBottom <= canvasHeight) || (pageTop <= 0 && pageBottom >= canvasHeight);
    }

    private PagesDrawingRequest GetPagesDrawingRequest()
    {
        if (Pages == null)
        {
            return null;
        }

        var drawingRequest = GetBaseDrawingRequest<PagesDrawingRequest>();
        drawingRequest.Pages = Pages;
        drawingRequest.VisiblePages = GetVisiblePages().ToArray();
        drawingRequest.BackgroundColor = BackgroundColor;
        drawingRequest.MaxThumbnailSize = MaxThumbnailSize;

        return drawingRequest;
    }

    private T GetBaseDrawingRequest<T>() where T : DrawingRequest, new()
    {
        return new T
        {
            Scale = Scale,
            Offset = new SKPoint(HorizontalOffset, VerticalOffset),
            CanvasSize = new SKSize(Width, Height),
            RenderTarget = _renderTargetFactory.GetRenderTarget(this),
        };
    }


    public void Update()
    {
        var pageCount = Pages.Count;

        var maxPageWidth = 0f;
        var totalHeight = 0f;

        foreach (var page in Pages)
        {
            var rotatedSize = page.Info.GetRotatedSize(page.UserRotation);
            maxPageWidth = Math.Max(maxPageWidth, rotatedSize.Width);
            totalHeight += rotatedSize.Height;
        }

        if (pageCount > 1)
        {
            totalHeight += MinimumPageGap * (pageCount - 1);
        }

        ExtentWidth = (maxPageWidth + PagesPadding.Left + PagesPadding.Right) * Scale;
        ExtentHeight = (PagesPadding.Top + totalHeight + PagesPadding.Bottom) * Scale;

        float horizontalOffset = (-HorizontalOffset + MathF.Max(0, (Width - ExtentWidth) / 2)) / Scale;
        float verticalOffset = -VerticalOffset / Scale + PagesPadding.Top;

        for (int i = 0; i < pageCount; i++)
        {
            var page = Pages[i];
            var rotatedSize = page.Info.GetRotatedSize(page.UserRotation);
            var pageOffsetLeft = (ExtentWidth / Scale - rotatedSize.Width) / 2;
            page.Offset = new SKPoint(horizontalOffset + pageOffsetLeft, verticalOffset);
            verticalOffset += rotatedSize.Height + MinimumPageGap;
        }
    }

    public void Render()
    {
        Debug.WriteLine($"HorizontalOffset: {HorizontalOffset};VerticalOffset: {VerticalOffset};Scale: {Scale}");
        var request = GetPagesDrawingRequest();
        _pdfRenderingQueue.EnqueueDrawingRequest(request);
    }
}

internal readonly struct VisiblePageInfo
{
    public VisiblePageInfo(int pageNumber, SKPoint offset, PageInfo pageInfo, int userRotation)
    {
        PageNumber = pageNumber;
        Offset = offset;
        Info = pageInfo;

        var normalizedUserRotation = userRotation % 360;

        if (normalizedUserRotation < 0)
        {
            normalizedUserRotation += 360;
        }

        UserRotation = normalizedUserRotation;
    }

    /// <summary>
    /// Gets the page number.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Gets the offset of the page.
    /// Page offset is the unscaled distance from the top-left corner of the page to the top-left corner of the document.
    /// </summary>
    public SKPoint Offset { get; }

    /// <summary>
    /// Gets the page information.
    /// </summary>
    public PageInfo Info { get; }

    /// <summary>
    /// Gets the user rotation of the page.
    /// </summary>
    public int UserRotation { get; }

    /// <summary>
    /// Gets the total rotation of the page.
    /// </summary>
    public int TotalRotation => Info.GetTotalRotation(UserRotation);

    /// <summary>
    /// Gets the rotated size of the page.
    /// </summary>
    public SKSize RotatedSize => Info.GetRotatedSize(UserRotation);

    /// <summary>
    /// Gets the unscaled bounds of the page.
    /// </summary>
    public SKRect Bounds => SKRect.Create(Offset, RotatedSize);

    /// <summary>
    /// Gets the scaled bounds of the page.
    /// </summary>
    /// <param name="scale">Scale factor.</param>
    /// <returns></returns>
    internal SKRect GetScaledBounds(double scale) => SKRect.Create((float)(Offset.X * scale), (float)(Offset.Y * scale), (float)(RotatedSize.Width * scale), (float)(RotatedSize.Height * scale));

    ///// <summary>
    ///// Converts the canvas position to the page position.
    ///// </summary>
    ///// <param name="canvasPosition">Position on canvas.</param>
    ///// <param name="scale">Scale factor.</param>
    ///// <param name="cropToMargins">If position will be cropped to page bounds.</param>
    ///// <returns>Position on page.</returns>
    //public Point ToPagePosition(Point canvasPosition, double scale, bool cropToMargins = false)
    //{
    //    SKPoint scaledPosition = new Point(canvasPosition.X / scale, canvasPosition.Y / scale);
    //    var result = new Point(scaledPosition.X - Offset.X, scaledPosition.Y - Offset.Y);

    //    if (cropToMargins)
    //    {
    //        if (result.X < 0)
    //        {
    //            result.X = 0;
    //        }
    //        if (result.Y < 0)
    //        {
    //            result.Y = 0;
    //        }
    //        if (result.X > RotatedSize.Width)
    //        {
    //            result.X = RotatedSize.Width;
    //        }
    //        if (result.Y > RotatedSize.Height)
    //        {
    //            result.Y = RotatedSize.Height;
    //        }
    //    }

    //    return result;
    //}

    ///// <summary>
    ///// Converts the page position to the canvas position.
    ///// </summary>
    ///// <param name="pagePosition">Page position.</param>
    ///// <param name="scale">Scale factor.</param>
    ///// <returns>Position on canvas.</returns>
    //public Point ToCanvasPosition(Point pagePosition, double scale)
    //{
    //    return new Point((pagePosition.X + Offset.X) * scale, (pagePosition.Y + Offset.Y) * scale);
    //}

    ///// <summary>
    ///// Returns the matrix that represents the rotation of the page.
    ///// </summary>
    ///// <param name="width">Page width.</param>
    ///// <param name="height">Page height.</param>
    ///// <param name="rotation">Rotation in degrees.</param>
    ///// <returns></returns>
    //public static Matrix GetPageRotationMatrix(double width, double height, int rotation)
    //{
    //    rotation = rotation % 360;

    //    if (rotation < 0)
    //    {
    //        rotation += 360;
    //    }

    //    Matrix matrix = new Matrix();
    //    matrix.Rotate(-rotation);

    //    switch (rotation)
    //    {
    //        case 90:
    //            matrix.Translate(0, height);
    //            break;
    //        case 180:
    //            matrix.Translate(width, height);
    //            break;
    //        case 270:
    //            matrix.Translate(width, 0);
    //            break;
    //    }

    //    return matrix;
    //}
}
