using PdfPixel.Annotations.Models;
using PdfPixel.Models;
using SkiaSharp;
using System;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Utility class for drawing line endings on annotations.
/// </summary>
/// <remarks>
/// Provides reusable methods for drawing various line ending styles (arrows, circles, squares, etc.)
/// used by Line, PolyLine, and other annotation types.
/// </remarks>
internal static class PdfAnnotationLineEndingRenderer
{
    /// <summary>
    /// Draws a line ending at the specified position.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="x">The X coordinate of the line ending.</param>
    /// <param name="y">The Y coordinate of the line ending.</param>
    /// <param name="otherX">The X coordinate of the next point (used for angle calculation).</param>
    /// <param name="otherY">The Y coordinate of the next point (used for angle calculation).</param>
    /// <param name="style">The line ending style.</param>
    /// <param name="lineWidth">The line width for rendering.</param>
    /// <param name="lineColor">The line color.</param>
    /// <param name="interiorColor">The interior fill color for closed shapes.</param>
    public static void DrawLineEnding(
        SKCanvas canvas,
        float x,
        float y,
        float otherX,
        float otherY,
        Models.PdfLineEndingStyle style,
        float lineWidth,
        SKColor lineColor,
        SKColor? interiorColor)
    {
        var dx = otherX - x;
        var dy = otherY - y;
        var angle = (float)Math.Atan2(dy, dx);
        var endingSize = lineWidth * 3;

        canvas.Save();
        canvas.Translate(x, y);
        canvas.RotateDegrees(angle * 180 / (float)Math.PI);

        switch (style)
        {
            case Models.PdfLineEndingStyle.OpenArrow:
                DrawOpenArrow(canvas, endingSize, lineWidth, lineColor);
                break;
            case Models.PdfLineEndingStyle.ClosedArrow:
                DrawClosedArrow(canvas, endingSize, lineWidth, lineColor, interiorColor);
                break;
            case Models.PdfLineEndingStyle.Square:
                DrawSquare(canvas, endingSize, lineWidth, lineColor, interiorColor);
                break;
            case Models.PdfLineEndingStyle.Circle:
                DrawCircle(canvas, endingSize, lineWidth, lineColor, interiorColor);
                break;
            case Models.PdfLineEndingStyle.Diamond:
                DrawDiamond(canvas, endingSize, lineWidth, lineColor, interiorColor);
                break;
            case Models.PdfLineEndingStyle.Butt:
                DrawButt(canvas, endingSize, lineWidth, lineColor);
                break;
            case Models.PdfLineEndingStyle.ROpenArrow:
                DrawROpenArrow(canvas, endingSize, lineWidth, lineColor);
                break;
            case Models.PdfLineEndingStyle.RClosedArrow:
                DrawRClosedArrow(canvas, endingSize, lineWidth, lineColor, interiorColor);
                break;
            case Models.PdfLineEndingStyle.Slash:
                DrawSlash(canvas, endingSize, lineWidth, lineColor);
                break;
        }

        canvas.Restore();
    }

    private static void DrawOpenArrow(SKCanvas canvas, float size, float lineWidth, SKColor color)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };

        using var path = new SKPath();
        path.MoveTo(size, -size / 2);
        path.LineTo(0, 0);
        path.LineTo(size, size / 2);

        canvas.DrawPath(path, paint);
    }

    private static void DrawClosedArrow(SKCanvas canvas, float size, float lineWidth, SKColor color, SKColor? interiorColor)
    {
        using var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo(size, -size / 2);
        path.LineTo(size, size / 2);
        path.Close();

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = interiorColor.Value
            };
            canvas.DrawPath(path, fillPaint);
        }

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };
        canvas.DrawPath(path, strokePaint);
    }

    private static void DrawSquare(SKCanvas canvas, float size, float lineWidth, SKColor color, SKColor? interiorColor)
    {
        var rect = new SKRect(-size / 2, -size / 2, size / 2, size / 2);

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = interiorColor.Value
            };
            canvas.DrawRect(rect, fillPaint);
        }

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };
        canvas.DrawRect(rect, strokePaint);
    }

    private static void DrawCircle(SKCanvas canvas, float size, float lineWidth, SKColor color, SKColor? interiorColor)
    {
        var radius = size / 2;

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = interiorColor.Value
            };
            canvas.DrawCircle(0, 0, radius, fillPaint);
        }

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };
        canvas.DrawCircle(0, 0, radius, strokePaint);
    }

    private static void DrawDiamond(SKCanvas canvas, float size, float lineWidth, SKColor color, SKColor? interiorColor)
    {
        using var path = new SKPath();
        path.MoveTo(size / 2, 0);
        path.LineTo(0, -size / 2);
        path.LineTo(-size / 2, 0);
        path.LineTo(0, size / 2);
        path.Close();

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = interiorColor.Value
            };
            canvas.DrawPath(path, fillPaint);
        }

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };
        canvas.DrawPath(path, strokePaint);
    }

    private static void DrawButt(SKCanvas canvas, float size, float lineWidth, SKColor color)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };

        canvas.DrawLine(0, -size / 2, 0, size / 2, paint);
    }

    private static void DrawROpenArrow(SKCanvas canvas, float size, float lineWidth, SKColor color)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };

        using var path = new SKPath();
        path.MoveTo(size, size / 2);
        path.LineTo(0, 0);
        path.LineTo(size, -size / 2);

        canvas.DrawPath(path, paint);
    }

    private static void DrawRClosedArrow(SKCanvas canvas, float size, float lineWidth, SKColor color, SKColor? interiorColor)
    {
        using var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo(size, size / 2);
        path.LineTo(size, -size / 2);
        path.Close();

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            using var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = interiorColor.Value
            };
            canvas.DrawPath(path, fillPaint);
        }

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };
        canvas.DrawPath(path, strokePaint);
    }

    private static void DrawSlash(SKCanvas canvas, float size, float lineWidth, SKColor color)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };

        canvas.DrawLine(-size / 2, -size / 2, size / 2, size / 2, paint);
    }
}
