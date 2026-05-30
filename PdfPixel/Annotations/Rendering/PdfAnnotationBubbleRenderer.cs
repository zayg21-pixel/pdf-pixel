using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Handles rendering of bubble indicators for annotations with content.
/// </summary>
internal static class PdfAnnotationBubbleRenderer
{
    private const float NormalBorderWidth = 1.0f;
    private const float HoverBorderWidth = 1.5f;

    /// <summary>
    /// Renders a bubble indicator for an annotation.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="annotation">The annotation to render a bubble for.</param>
    /// <param name="page">The PDF page containing the annotation.</param>
    /// <param name="visualStateKind">The visual state (Normal, Rollover, Down).</param>
    public static void RenderBubble(
        IPdfCommandProcessor processor,
        PdfAnnotationBase annotation,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind)
    {
        SKRect bubbleRect = annotation.GetHoverRectangle(page);

        bool isHovered = (visualStateKind & PdfAnnotationVisualStateKind.Rollover) != 0
            || (visualStateKind & PdfAnnotationVisualStateKind.Down) != 0;

        float currentBorderWidth = isHovered ? HoverBorderWidth : NormalBorderWidth;
        float cornerRadius = bubbleRect.Width * 0.25f;

        SKColor backgroundColor = annotation.ResolveInteriorColor(page, new SKColor(255, 255, 235));
        SKColor borderColor = annotation.ResolveColor(page, new SKColor(180, 140, 60));

        DrawSpeechBubble(processor, bubbleRect, backgroundColor, borderColor, currentBorderWidth, cornerRadius, isHovered);
    }

    /// <summary>
    /// Draws a standard speech bubble with a rounded rectangle and a bottom-center tail.
    /// </summary>
    private static void DrawSpeechBubble(
        IPdfCommandProcessor processor,
        SKRect bubbleRect,
        in SKColor backgroundColor,
        in SKColor borderColor,
        float borderWidth,
        float cornerRadius,
        bool isHovered)
    {
        float tailHeight = bubbleRect.Height * 0.2f;
        if (tailHeight <= 0)
        {
            return;
        }

        float rectTop = bubbleRect.Top + tailHeight;
        float rectBottom = bubbleRect.Bottom;

        SKRect rect = new(
            bubbleRect.Left,
            rectTop,
            bubbleRect.Right,
            rectBottom);

        using SKPath path = new();

        path.AddRoundRect(rect, cornerRadius, cornerRadius);

        float tailWidth = bubbleRect.Width * 0.4f;
        float tailBaseY = bubbleRect.Top + tailHeight;
        float tailTipY = bubbleRect.Top;

        // Make tail visually angled to the right: tip shifted right, left base wider.
        float tailTipX = bubbleRect.MidX + (bubbleRect.Width * 0.3f);
        float tailRightX = tailTipX + (tailWidth * 0.1f);
        float tailLeftX = tailTipX - (tailWidth * 0.9f);

        path.MoveTo(tailTipX, tailTipY);
        path.LineTo(tailRightX, tailBaseY);
        path.LineTo(tailLeftX, tailBaseY);
        path.Close();

        if (isHovered)
        {
            SKPaint shadowPaint = new()
            {
                Style = SKPaintStyle.Fill,
                Color = borderColor.WithAlpha(40),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
            };

            processor.Process(new DrawPathCommand(path, shadowPaint));
        }

        SKPaint fillPaint = new()
        {
            Style = SKPaintStyle.Fill,
            Color = backgroundColor
        };

        processor.Process(new DrawPathCommand(path, fillPaint));

        SKPaint strokePaint = new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth,
            Color = borderColor
        };

        processor.Process(new DrawPathCommand(path, strokePaint));

        float contentMargin = bubbleRect.Width * 0.15f;
        float lineStartX = rect.Left + contentMargin;
        float lineEndX = rect.Right - contentMargin;

        float availableHeight = rectBottom - rectTop;
        if (availableHeight > 0)
        {
            float firstLineY = rectTop + (availableHeight * 0.33f);
            float secondLineY = rectTop + (availableHeight * 0.66f);

            SKPaint contentPaint1 = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth * 0.7f,
                Color = borderColor.WithAlpha(180),
                StrokeCap = SKStrokeCap.Round
            };

            using SKPath linePath1 = new();
            linePath1.MoveTo(lineStartX, firstLineY);
            linePath1.LineTo(lineEndX, firstLineY);
            processor.Process(new DrawPathCommand(linePath1, contentPaint1));

            SKPaint contentPaint2 = new()
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth * 0.7f,
                Color = borderColor.WithAlpha(180),
                StrokeCap = SKStrokeCap.Round
            };

            using SKPath linePath2 = new();
            linePath2.MoveTo(lineStartX, secondLineY);
            linePath2.LineTo(lineEndX, secondLineY);
            processor.Process(new DrawPathCommand(linePath2, contentPaint2));
        }
    }
}
