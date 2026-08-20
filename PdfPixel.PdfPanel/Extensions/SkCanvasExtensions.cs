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
        PageDrawFlags flags,
        AnimationState? animation)
    {
        int savedCount = canvas.Save();
        PdfMatrix deviceMatrix = page.GetContentToCanvasMatrix(request.Scale);

        canvas.Concat(deviceMatrix.ToSkMatrix());

        PdfRectangle pageBounds = new(0, 0, page.Info.Width, page.Info.Height);
        SKRect pageRect = PdfCommandProcessingUtilities.SnapToWholeDevicePixels(pageBounds, deviceMatrix).ToSkRect();

        if ((flags & PageDrawFlags.Shadow) != 0)
        {
            DrawPageShadow(canvas, pageRect);
        }

        canvas.ClipRect(pageRect);

        if ((flags & PageDrawFlags.Background) != 0)
        {
            DrawPageBackground(canvas, pageRect);
        }

        if ((flags & PageDrawFlags.Placeholder) != 0 && animation != null)
        {
            DrawPlaceholder(canvas, pageRect, animation.Value);
        }

        if ((flags & PageDrawFlags.Content) != 0)
        {
            tiler.DrawTiles(canvas, in page, request.Scale, deviceMatrix);
            DrawPagePicture(canvas, pictures?.Annotations);
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

    private static void DrawPagePicture(SKCanvas canvas, ContentLocker<SKPicture>? content)
    {
        if (content?.HasContent != true)
        {
            return;
        }

        using LockedContent<SKPicture> contentPicture = content.GetContent();

        canvas.DrawPicture(contentPicture.Content);
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

    private static void DrawPageShadow(SKCanvas canvas, SKRect pageRect)
    {
        if (pageRect.Contains(canvas.LocalClipBounds))
        {
            return;
        }

        using SKPaint shadowPaint = new()
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
            Color = SKColors.Gray.WithAlpha(160),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f)
        };

        canvas.DrawRect(pageRect, shadowPaint);
    }

    private static void DrawPageBackground(SKCanvas canvas, SKRect pageRect)
    {
        using SKPaint paint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            IsAntialias = false
        };

        canvas.DrawRect(pageRect, paint);
    }
}
