using PdfPixel.Commands.Processing;
using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Text;
using PdfPixel.Skia;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Extensions;

internal static class SkCanvasExtensions
{
    public static void DrawPage(
        this SKCanvas canvas,
        in VisiblePageInfo page,
        PagesDrawingRequest request,
        PdfContentPictures pictures,
        PdfPageContentTiler tiler,
        PdfPanelTextSelector textSelector,
        float cornerRadius,
        PageDrawFlags flags,
        in AnimationState animation)
    {
        int savedCount = canvas.Save();
        PdfMatrix deviceMatrix = page.GetContentToCanvasMatrix(request.Scale);

        canvas.Concat(deviceMatrix.ToSkMatrix());

        PdfRectangle pageBounds = new(0, 0, page.Info.CropBox.Width, page.Info.CropBox.Height);
        SKRect pageRect = PdfCommandProcessingUtilities.SnapToWholeDevicePixels(pageBounds, deviceMatrix).ToSkRect();

        if ((flags & PageDrawFlags.Shadow) != 0)
        {
            DrawPageShadow(canvas, pageRect, cornerRadius);
        }

        ClipToPage(canvas, pageRect, cornerRadius);

        if ((flags & PageDrawFlags.Background) != 0)
        {
            DrawPageBackground(canvas, pageRect, cornerRadius);
        }

        if ((flags & PageDrawFlags.Placeholder) != 0)
        {
            DrawPlaceholder(canvas, pageRect, animation);
        }

        if ((flags & PageDrawFlags.Content) != 0)
        {
            tiler.DrawTiles(canvas, in page, request.Scale, deviceMatrix);
            DrawPagePicture(canvas, pictures?.Annotations, page.Info);
            DrawSelectionPicture(canvas, textSelector, page);
        }

        canvas.RestoreToCount(savedCount);
    }

    private static void DrawSelectionPicture(SKCanvas canvas, PdfPanelTextSelector textSelector, in VisiblePageInfo page)
    {
        SKPicture? picture = textSelector.GetSelectionPicture(page.PageNumber);
        if (picture == null)
        {
            return;
        }

        canvas.DrawPicture(picture);
    }

    private static void DrawPagePicture(SKCanvas canvas, ContentLocker<SKPicture>? content, in PdfPanelPageInfo pageInfo)
    {
        if (content?.HasContent != true)
        {
            return;
        }

        using LockedContent<SKPicture> contentPicture = content.GetContent();
        SKPicture? picture = contentPicture.Content;

        if (picture == null)
        {
            return;
        }

        // The canvas is in page space, so a picture recorded at scale is brought back to it.
        SKRect cullRect = picture.CullRect;

        int savedCount = canvas.Save();
        canvas.Scale(pageInfo.CropBox.Width / cullRect.Width, pageInfo.CropBox.Height / cullRect.Height);

        canvas.DrawPicture(picture);

        canvas.RestoreToCount(savedCount);
    }

    private static void DrawPlaceholder(SKCanvas canvas, SKRect pageRect, in AnimationState animation)
    {
        float minDimension = Math.Min(pageRect.Width, pageRect.Height);
        float radius = minDimension * 0.05f;
        float strokeWidth = radius * 0.15f;
        float centerX = pageRect.MidX;
        float centerY = pageRect.MidY;

        SKRect arcRect = new(centerX - radius, centerY - radius, centerX + radius, centerY + radius);
        float startAngle = (animation.Tick % animation.Fps) / (float)animation.Fps * 360f;

        using SKPaint paint = new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = strokeWidth,
            Color = new SKColor(0, 0, 0, 80),
            IsAntialias = true,
            StrokeCap = SKStrokeCap.Round
        };

        canvas.DrawArc(arcRect, startAngle, 270f, false, paint);
    }

    private static void ClipToPage(SKCanvas canvas, SKRect pageRect, float cornerRadius)
    {
        if (cornerRadius <= 0)
        {
            canvas.ClipRect(pageRect);
            return;
        }

        using SKRoundRect roundRect = new(pageRect, cornerRadius, cornerRadius);

        canvas.ClipRoundRect(roundRect, SKClipOperation.Intersect, antialias: true);
    }

    private static void DrawPageShadow(SKCanvas canvas, SKRect pageRect, float cornerRadius)
    {
        if (pageRect.Contains(canvas.LocalClipBounds))
        {
            return;
        }

        using SKPaint shadowPaint = new()
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = cornerRadius > 0,
            Color = SKColors.Gray.WithAlpha(160),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f)
        };

        DrawPageRect(canvas, pageRect, cornerRadius, shadowPaint);
    }

    private static void DrawPageBackground(SKCanvas canvas, SKRect pageRect, float cornerRadius)
    {
        using SKPaint paint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            IsAntialias = cornerRadius > 0
        };

        DrawPageRect(canvas, pageRect, cornerRadius, paint);
    }

    private static void DrawPageRect(SKCanvas canvas, SKRect pageRect, float cornerRadius, SKPaint paint)
    {
        if (cornerRadius > 0)
        {
            canvas.DrawRoundRect(pageRect, cornerRadius, cornerRadius, paint);
        }
        else
        {
            canvas.DrawRect(pageRect, paint);
        }
    }
}
