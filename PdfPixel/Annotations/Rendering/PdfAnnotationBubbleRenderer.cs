using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Handles rendering of bubble indicators for annotations with content.
/// </summary>
public static class PdfAnnotationBubbleRenderer
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
        PdfPage page,
        PdfAnnotationVisualStateKind visualStateKind)
    {
        var bubbleRect = annotation.GetHoverRectangle(page);

        var isHovered = visualStateKind.HasFlag(PdfAnnotationVisualStateKind.Rollover) ||
                        visualStateKind.HasFlag(PdfAnnotationVisualStateKind.Down);

        var currentBorderWidth = isHovered ? HoverBorderWidth : NormalBorderWidth;
        var cornerRadius = bubbleRect.Width * 0.25f;

        var backgroundColor = annotation.ResolveInteriorColor(page, new SKColor(255, 255, 235));
        var borderColor = annotation.ResolveColor(page, new SKColor(180, 140, 60));

        DrawSpeechBubble(processor, bubbleRect, backgroundColor, borderColor, currentBorderWidth, cornerRadius, isHovered);
    }

    /// <summary>
    /// Draws a standard speech bubble with a rounded rectangle and a bottom-center tail.
    /// </summary>
    public static void DrawSpeechBubble(
        IPdfCommandProcessor processor,
        SKRect bubbleRect,
        SKColor backgroundColor,
        SKColor borderColor,
        float borderWidth,
        float cornerRadius,
        bool isHovered)
    {
        var tailHeight = bubbleRect.Height * 0.2f;
        if (tailHeight <= 0)
        {
            return;
        }

        var rectTop = bubbleRect.Top + tailHeight;
        var rectBottom = bubbleRect.Bottom;

        var rect = new SKRect(
            bubbleRect.Left,
            rectTop,
            bubbleRect.Right,
            rectBottom);

        using var path = new SKPath();

        path.AddRoundRect(rect, cornerRadius, cornerRadius);

        var tailWidth = bubbleRect.Width * 0.4f;
        var tailBaseY = bubbleRect.Top + tailHeight;
        var tailTipY = bubbleRect.Top;

        // Make tail visually angled to the right: tip shifted right, left base wider.
        var tailTipX = bubbleRect.MidX + bubbleRect.Width * 0.3f;
        var tailRightX = tailTipX + tailWidth * 0.1f;
        var tailLeftX = tailTipX - tailWidth * 0.9f;

        path.MoveTo(tailTipX, tailTipY);
        path.LineTo(tailRightX, tailBaseY);
        path.LineTo(tailLeftX, tailBaseY);
        path.Close();

        if (isHovered)
        {
            var shadowPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                Color = borderColor.WithAlpha(40),
                IsAntialias = true,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 1.5f)
            };

            processor.Process(new DrawPathCommand(path, shadowPaint));
        }

        var fillPaint = new SKPaint
        {
            Style = SKPaintStyle.Fill,
            Color = backgroundColor,
            IsAntialias = true
        };

        processor.Process(new DrawPathCommand(path, fillPaint));

        var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = borderWidth,
            Color = borderColor,
            IsAntialias = true
        };

        processor.Process(new DrawPathCommand(path, strokePaint));

        var contentMargin = bubbleRect.Width * 0.15f;
        var lineStartX = rect.Left + contentMargin;
        var lineEndX = rect.Right - contentMargin;

        var availableHeight = rectBottom - rectTop;
        if (availableHeight > 0)
        {
            var firstLineY = rectTop + availableHeight * 0.33f;
            var secondLineY = rectTop + availableHeight * 0.66f;

            var contentPaint1 = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth * 0.7f,
                Color = borderColor.WithAlpha(180),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            using var linePath1 = new SKPath();
            linePath1.MoveTo(lineStartX, firstLineY);
            linePath1.LineTo(lineEndX, firstLineY);
            processor.Process(new DrawPathCommand(linePath1, contentPaint1));

            var contentPaint2 = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = borderWidth * 0.7f,
                Color = borderColor.WithAlpha(180),
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round
            };

            using var linePath2 = new SKPath();
            linePath2.MoveTo(lineStartX, secondLineY);
            linePath2.LineTo(lineEndX, secondLineY);
            processor.Process(new DrawPathCommand(linePath2, contentPaint2));
        }
    }
}
