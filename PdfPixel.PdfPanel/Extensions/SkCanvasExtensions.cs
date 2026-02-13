using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Linq;

namespace PdfPixel.PdfPanel.Extensions;

[Flags]
internal enum PageDrawFlags
{
    Content = 1,
    Shadow = 2,
    Background = 4,
    Thumbnail = 8
}

/// <summary>
/// Extensions for drawing PDF data on <see cref="SKCanvas"/>.
/// </summary>
internal static class SkCanvasExtensions
{
    public static void DrawPageFromRequest(this SKCanvas canvas, int pageNumber, PagesDrawingRequest request, PageDrawFlags drawFlags)
    {
        if (!request.VisiblePages.Any(x => x.PageNumber == pageNumber))
        {
            return;
        }

        var page = request.VisiblePages.First(x => x.PageNumber == pageNumber);

        int savedPageCount = canvas.Save();
        canvas.Scale((float)request.Scale, (float)request.Scale);
        canvas.Translate((float)page.Offset.X, (float)page.Offset.Y);

        if (drawFlags.HasFlag(PageDrawFlags.Shadow))
        {
            DrawPageShadow(canvas, page, request.PageCornerRadius);
        }

        if (drawFlags.HasFlag(PageDrawFlags.Background))
        {
            DrawPageBackground(canvas, page, request.PageCornerRadius);
        }

        var pageRectangle = new SKRect(0, 0, page.RotatedSize.Width, page.RotatedSize.Height);

        if (request.PageCornerRadius > 0)
        {
            using var clipPath = new SKPath();
            clipPath.AddRoundRect(pageRectangle, request.PageCornerRadius, request.PageCornerRadius);
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: true);
        }

        canvas.SaveLayer(pageRectangle, default);

        if (!request.Pages.TryGetPictureFromCache(page.PageNumber, out var picture))
        {
            canvas.RestoreToCount(savedPageCount);
            return;
        }

        if (drawFlags.HasFlag(PageDrawFlags.Thumbnail))
        {
            DrawCachedThumbnail(canvas, picture, page);
        }

        if (drawFlags.HasFlag(PageDrawFlags.Content))
        {
            DrawCachedPicture(canvas, picture, page);
            DrawCachedAnnotationPicture(canvas, picture, page);
        }

        canvas.RestoreToCount(savedPageCount);
    }

    private static void DrawCachedPicture(SKCanvas canvas, CachedSkPicture picture, VisiblePageInfo page)
    {
        lock (picture.DisposeLocker)
        {
            if (picture.IsDisposed || picture.Picture == null)
            {
                return;
            }

            var transformMatrix = GetPictureTransformMatrix(picture.Picture.CullRect.Width, picture.Picture.CullRect.Height, page.Info, page.UserRotation);
            canvas.DrawPicture(picture.Picture, in transformMatrix);
        }
    }

    private static void DrawCachedAnnotationPicture(SKCanvas canvas, CachedSkPicture picture, VisiblePageInfo page)
    {
        lock (picture.DisposeLocker)
        {
            if (picture.IsDisposed || picture.AnnotationPicture == null)
            {
                return;
            }

            var transformMatrix = GetPictureTransformMatrix(picture.AnnotationPicture.CullRect.Width, picture.AnnotationPicture.CullRect.Height, page.Info, page.UserRotation);
            canvas.DrawPicture(picture.AnnotationPicture, in transformMatrix);
        }
    }

    private static void DrawCachedThumbnail(SKCanvas canvas, CachedSkPicture picture, VisiblePageInfo page)
    {
        lock (picture.DisposeLocker)
        {
            if (picture.IsDisposed)
            {
                return;
            }

            var thumbnail = picture.Thumbnail;

            if (thumbnail == null)
            {
                return;
            }

            int saveCount = canvas.Save();
            var thumbnailRect = SKRect.Create(0, 0, thumbnail.Width, thumbnail.Height);
            var destRect = SKRect.Create(0, 0, page.Info.Width, page.Info.Height);
            var transformMatrix = GetRotationTranslationMatrix(page.Info, page.UserRotation);
            canvas.Concat(in transformMatrix);

            var samplingOption = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            canvas.DrawImage(thumbnail, thumbnailRect, destRect, samplingOption);
            canvas.RestoreToCount(saveCount);
        }
    }

    private static SKMatrix GetRotationTranslationMatrix(PdfPanelPageInfo pageInfo, int userRotation)
    {
        var rotation = pageInfo.GetTotalRotation(userRotation);
        var infoWidth = pageInfo.Width;
        var infoHeight = pageInfo.Height;

        var matrixRotation = SKMatrix.CreateRotationDegrees(rotation);

        var translateX = rotation switch
        {
            180 => -infoWidth,
            270 => -infoWidth,
            _ => 0
        };
        var translateY = rotation switch
        {
            90 => -infoHeight,
            180 => -infoHeight,
            _ => 0
        };

        var matrixTranslation = SKMatrix.CreateTranslation((float)translateX, (float)translateY);

        return SKMatrix.Concat(matrixRotation, matrixTranslation);
    }

    public static SKMatrix GetPictureTransformMatrix(float pictureWidth, float pictureHeight, PdfPanelPageInfo pageInfo, int userRotation)
    {
        var infoWidth = pageInfo.Width;
        var infoHeight = pageInfo.Height;

        var matrixScale = SKMatrix.CreateScale((float)(infoWidth / pictureWidth), (float)(infoHeight / pictureHeight));
        var matrixRotationTranslation = GetRotationTranslationMatrix(pageInfo, userRotation);

        return SKMatrix.Concat(matrixRotationTranslation, matrixScale);
    }

    private static void DrawPageShadow(SKCanvas canvas, VisiblePageInfo page, float cornerRadius)
    {
        var rotatedSize = page.RotatedSize;
        var pageRectangle = new SKRect(0, 0, rotatedSize.Width, rotatedSize.Height);

        if (!pageRectangle.Contains(canvas.LocalClipBounds))
        {
            const float ShadowSigma = 3f;
            const byte ShadowAlpha = 160;

            using var shadowPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = SKColors.Gray.WithAlpha(ShadowAlpha),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, ShadowSigma)
            };

            int saveCount = canvas.Save();

            if (cornerRadius > 0)
            {
                canvas.DrawRoundRect(pageRectangle, cornerRadius, cornerRadius, shadowPaint);
            }
            else
            {
                canvas.DrawRect(pageRectangle, shadowPaint);
            }

            canvas.RestoreToCount(saveCount);
        }
    }

    private static void DrawPageBackground(SKCanvas canvas, VisiblePageInfo page, float cornerRadius)
    {
        var rotatedSize = page.RotatedSize;
        var pageRectangle = new SKRect(0, 0, rotatedSize.Width, rotatedSize.Height);

        using var backgroundFill = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            IsAntialias = true
        };

        if (cornerRadius > 0)
        {
            canvas.DrawRoundRect(pageRectangle, cornerRadius, cornerRadius, backgroundFill);
        }
        else
        {
            canvas.DrawRect(pageRectangle, backgroundFill);
        }
    }
}
