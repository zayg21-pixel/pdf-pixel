using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;
using System.Linq;
using System.Threading;

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
    public static void DrawPageFromRequest(this SKCanvas canvas, int pageNumber, PagesDrawingRequest request, PdfPanelRenderCommand command, PageDrawFlags drawFlags, CancellationToken cancellationToken)
    {
        if (!request.VisiblePages.Any(x => x.PageNumber == pageNumber))
        {
            return;
        }

        var page = request.VisiblePages.First(x => x.PageNumber == pageNumber);

        int savedPageCount = canvas.Save();
        try
        {
            canvas.Scale((float)request.Scale, (float)request.Scale);
            canvas.Translate((float)page.Offset.X, (float)page.Offset.Y);

            if (drawFlags.HasFlag(PageDrawFlags.Shadow))
            {
                DrawPageShadow(canvas, page, request.RenderingParameters.Antialias, request.PageCornerRadius);
            }

            if (drawFlags.HasFlag(PageDrawFlags.Background))
            {
                DrawPageBackground(canvas, page, request.RenderingParameters.Antialias, request.PageCornerRadius);
            }

            var pageRectangle = new SKRect(0, 0, page.RotatedSize.Width, page.RotatedSize.Height);

            if (request.PageCornerRadius > 0)
            {
                using var clipPath = new SKPath();
                clipPath.AddRoundRect(pageRectangle, request.PageCornerRadius, request.PageCornerRadius);
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: request.RenderingParameters.Antialias);
            }

            // TODO: [HIGH] this consume extra resources and so far commented out. Need to find a way to render on top of white background without SaveLayer.
            //canvas.SaveLayer(pageRectangle, default);
            //canvas.Clear();

            //if (!request.Pages.TryGetPictureFromCache(page.PageNumber, out var picture))
            //{
            //    return;
            //}

            //if (drawFlags.HasFlag(PageDrawFlags.Thumbnail))
            //{
            //    DrawCachedThumbnail(canvas, picture, page);
            //}

            if (drawFlags.HasFlag(PageDrawFlags.Content))
            {
                if (command.ContentPictures != null)
                {
                    DrawPagePicture(canvas, command.ContentPictures.Content, page, cancellationToken);
                    DrawPagePicture(canvas, command.ContentPictures.Annotations, page, cancellationToken);
                }
            }
        }
        finally
        {
            canvas.RestoreToCount(savedPageCount);
        }
    }

    private static void DrawPagePicture(SKCanvas canvas, ContentLocker<SKPicture> content, VisiblePageInfo page, CancellationToken cancellationToken)
    {
        if (content == null)
        {
            return;
        }

        if (!content.HasContent)
        {
            return;
        }

        using var contentPicture = content.GetContent();

        // Recordings are at scale 1 (page coordinate space).
        var transformMatrix = GetPictureTransformMatrix(page.Info.Width, page.Info.Height, page.Info, page.UserRotation);
        int saveCount = canvas.Save();
        try
        {
            canvas.Concat(in transformMatrix);
            canvas.DrawPicture(contentPicture.Content);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

    private static void DrawCachedThumbnail(SKCanvas canvas, SKImage thumbnail, VisiblePageInfo page)
    {
        // TODO: remove if not needed
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

    private static void DrawPageShadow(SKCanvas canvas, VisiblePageInfo page, bool antialias, float cornerRadius)
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
                IsAntialias = antialias,
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

    private static void DrawPageBackground(SKCanvas canvas, VisiblePageInfo page, bool antialias, float cornerRadius)
    {
        var rotatedSize = page.RotatedSize;
        var pageRectangle = new SKRect(0, 0, rotatedSize.Width, rotatedSize.Height);

        using var backgroundFill = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            IsAntialias = antialias,
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
