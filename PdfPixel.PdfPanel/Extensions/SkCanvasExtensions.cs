using PdfPixel.PdfPanel.Animation;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Extensions;

internal static class SkCanvasExtensions
{
    public static void DrawPage(this SKCanvas canvas, in VisiblePageInfo page, PagesDrawingRequest request, PdfContentPictures pictures, PageDrawFlags flags, AnimationState? animation)
    {
        int savedCount = canvas.Save();
        try
        {
            canvas.Scale(request.Scale, request.Scale);
            canvas.Translate(page.Offset.X, page.Offset.Y);

            SKRect pageRect = new(0, 0, page.RotatedSize.Width, page.RotatedSize.Height);


            if ((flags & PageDrawFlags.Shadow) != 0)
            {
                DrawPageShadow(canvas, page, request.RenderingParameters.Antialias, request.PageCornerRadius);
            }

            if (request.PageCornerRadius > 0)
            {
                using SKPath clipPath = new();
                clipPath.AddRoundRect(pageRect, request.PageCornerRadius, request.PageCornerRadius);
                canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: request.RenderingParameters.Antialias);
            }
            else
            {
                canvas.ClipRect(pageRect);
            }

            if ((flags & PageDrawFlags.Background) != 0)
            {
                DrawPageBackground(canvas, page, request.RenderingParameters.Antialias, request.PageCornerRadius);
            }

            if ((flags & PageDrawFlags.Content) != 0)
            {
                if (pictures?.Content?.HasContent == true)
                {
                    DrawPagePicture(canvas, pictures.Content, page);
                    DrawPagePicture(canvas, pictures.Annotations, page);
                }
                else if ((flags & PageDrawFlags.Placeholder) != 0 && animation != null)
                {
                    DrawPlaceholder(canvas, page, animation.Value);
                }
            }
        }
        finally
        {
            canvas.RestoreToCount(savedCount);
        }
    }

    private static void DrawPagePicture(SKCanvas canvas, ContentLocker<SKPicture>? content, in VisiblePageInfo page)
    {
        if (content?.HasContent != true)
        {
            return;
        }

        using LockedContent<SKPicture> contentPicture = content.GetContent();

        SKMatrix transform = page.ContentTransform;
        int saveCount = canvas.Save();
        try
        {
            canvas.Concat(in transform);
            canvas.DrawPicture(contentPicture.Content);
        }
        finally
        {
            canvas.RestoreToCount(saveCount);
        }
    }

    private static void DrawPlaceholder(SKCanvas canvas, in VisiblePageInfo page, in AnimationState animation)
    {
        float minDimension = Math.Min(page.RotatedSize.Width, page.RotatedSize.Height);
        float radius = minDimension * 0.05f;
        float strokeWidth = radius * 0.15f;
        float cx = page.RotatedSize.Width / 2f;
        float cy = page.RotatedSize.Height / 2f;

        SKRect arcRect = new(cx - radius, cy - radius, cx + radius, cy + radius);
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

    private static void DrawPageShadow(SKCanvas canvas, in VisiblePageInfo page, bool antialias, float cornerRadius)
    {
        SKRect pageRect = new(0, 0, page.RotatedSize.Width, page.RotatedSize.Height);

        if (pageRect.Contains(canvas.LocalClipBounds))
        {
            return;
        }

        using SKPaint shadowPaint = new()
        {
            Style = SKPaintStyle.Fill,
            IsAntialias = antialias,
            Color = SKColors.Gray.WithAlpha(160),
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 3f)
        };

        int saveCount = canvas.Save();

        if (cornerRadius > 0)
        {
            canvas.DrawRoundRect(pageRect, cornerRadius, cornerRadius, shadowPaint);
        }
        else
        {
            canvas.DrawRect(pageRect, shadowPaint);
        }

        canvas.RestoreToCount(saveCount);
    }

    private static void DrawPageBackground(SKCanvas canvas, in VisiblePageInfo page, bool antialias, float cornerRadius)
    {
        SKRect pageRect = new(0, 0, page.RotatedSize.Width, page.RotatedSize.Height);

        using SKPaint paint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = SKColors.White,
            IsAntialias = antialias
        };

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
