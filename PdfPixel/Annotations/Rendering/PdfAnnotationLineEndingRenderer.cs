using PdfPixel.Annotations.Models;
using PdfPixel.Commands;
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
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="x">The X coordinate of the line ending.</param>
    /// <param name="y">The Y coordinate of the line ending.</param>
    /// <param name="otherX">The X coordinate of the next point (used for angle calculation).</param>
    /// <param name="otherY">The Y coordinate of the next point (used for angle calculation).</param>
    /// <param name="style">The line ending style.</param>
    /// <param name="lineWidth">The line width for rendering.</param>
    /// <param name="lineColor">The line color.</param>
    /// <param name="interiorColor">The interior fill color for closed shapes.</param>
    /// <param name="renderingParameters">Rendering parameters.</param>
    public static void DrawLineEnding(
        IPdfCommandProcessor processor,
        float x,
        float y,
        float otherX,
        float otherY,
        Models.PdfLineEndingStyle style,
        float lineWidth,
        SKColor lineColor,
        SKColor? interiorColor,
        PdfRenderingParameters renderingParameters)
    {
        var dx = otherX - x;
        var dy = otherY - y;
        var angle = (float)Math.Atan2(dy, dx);
        var endingSize = lineWidth * 3;

        processor.Process(new SaveStateCommand());
        processor.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(x, y)));
        processor.Process(new ConcatMatrixCommand(SKMatrix.CreateRotationDegrees(angle * 180 / (float)Math.PI)));

        switch (style)
        {
            case Models.PdfLineEndingStyle.OpenArrow:
                DrawOpenArrow(processor, endingSize, lineWidth, lineColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.ClosedArrow:
                DrawClosedArrow(processor, endingSize, lineWidth, lineColor, interiorColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.Square:
                DrawSquare(processor, endingSize, lineWidth, lineColor, interiorColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.Circle:
                DrawCircle(processor, endingSize, lineWidth, lineColor, interiorColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.Diamond:
                DrawDiamond(processor, endingSize, lineWidth, lineColor, interiorColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.Butt:
                DrawButt(processor, endingSize, lineWidth, lineColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.ROpenArrow:
                DrawROpenArrow(processor, endingSize, lineWidth, lineColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.RClosedArrow:
                DrawRClosedArrow(processor, endingSize, lineWidth, lineColor, interiorColor, renderingParameters);
                break;
            case Models.PdfLineEndingStyle.Slash:
                DrawSlash(processor, endingSize, lineWidth, lineColor, renderingParameters);
                break;
        }

        processor.Process(new RestoreStateCommand());
    }

    private static void DrawOpenArrow(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, PdfRenderingParameters renderingParameters)
    {
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = renderingParameters.Antialias,
            Color = color
        };

        using var path = new SKPath();
        path.MoveTo(size, -size / 2);
        path.LineTo(0, 0);
        path.LineTo(size, size / 2);

        processor.Process(new DrawPathCommand(path, paint));
    }

    private static void DrawClosedArrow(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, SKColor? interiorColor, PdfRenderingParameters renderingParameters)
    {
        using var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo(size, -size / 2);
        path.LineTo(size, size / 2);
        path.Close();

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = renderingParameters.Antialias,
                Color = interiorColor.Value
            };
            processor.Process(new DrawPathCommand(path, fillPaint));
        }

        var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = renderingParameters.Antialias,
            Color = color
        };
        processor.Process(new DrawPathCommand(path, strokePaint));
    }

    private static void DrawSquare(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, SKColor? interiorColor, PdfRenderingParameters renderingParameters)
    {
        var rect = new SKRect(-size / 2, -size / 2, size / 2, size / 2);

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = renderingParameters.Antialias,
                Color = interiorColor.Value
            };
            using var fillPath = new SKPath();
            fillPath.AddRect(rect);
            processor.Process(new DrawPathCommand(fillPath, fillPaint));
        }

        var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };
        using var strokePath = new SKPath();
        strokePath.AddRect(rect);
        processor.Process(new DrawPathCommand(strokePath, strokePaint));
    }

    private static void DrawCircle(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, SKColor? interiorColor, PdfRenderingParameters renderingParameters)
    {
        var radius = size / 2;

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = renderingParameters.Antialias,
                Color = interiorColor.Value
            };
            using var fillPath = new SKPath();
            fillPath.AddCircle(0, 0, radius);
            processor.Process(new DrawPathCommand(fillPath, fillPaint));
        }

        var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = true,
            Color = color
        };
        using var strokePath = new SKPath();
        strokePath.AddCircle(0, 0, radius);
        processor.Process(new DrawPathCommand(strokePath, strokePaint));
    }

    private static void DrawDiamond(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, SKColor? interiorColor, PdfRenderingParameters renderingParameters)
    {
        using var path = new SKPath();
        path.MoveTo(size / 2, 0);
        path.LineTo(0, -size / 2);
        path.LineTo(-size / 2, 0);
        path.LineTo(0, size / 2);
        path.Close();

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = renderingParameters.Antialias,
                Color = interiorColor.Value
            };
            processor.Process(new DrawPathCommand(path, fillPaint));
        }

        var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = renderingParameters.Antialias,
            Color = color
        };
        processor.Process(new DrawPathCommand(path, strokePaint));
    }

    private static void DrawButt(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, PdfRenderingParameters renderingParameters)
    {
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = renderingParameters.Antialias,
            Color = color
        };

        using var path = new SKPath();
        path.MoveTo(0, -size / 2);
        path.LineTo(0, size / 2);
        processor.Process(new DrawPathCommand(path, paint));
    }

    private static void DrawROpenArrow(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, PdfRenderingParameters renderingParameters)
    {
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = renderingParameters.Antialias,
            Color = color
        };

        using var path = new SKPath();
        path.MoveTo(size, size / 2);
        path.LineTo(0, 0);
        path.LineTo(size, -size / 2);

        processor.Process(new DrawPathCommand(path, paint));
    }

    private static void DrawRClosedArrow(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, SKColor? interiorColor, PdfRenderingParameters renderingParameters)
    {
        using var path = new SKPath();
        path.MoveTo(0, 0);
        path.LineTo(size, size / 2);
        path.LineTo(size, -size / 2);
        path.Close();

        if (interiorColor.HasValue && interiorColor.Value != SKColors.Transparent)
        {
            var fillPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = renderingParameters.Antialias,
                Color = interiorColor.Value
            };
            processor.Process(new DrawPathCommand(path, fillPaint));
        }

        var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = renderingParameters.Antialias,
            Color = color
        };
        processor.Process(new DrawPathCommand(path, strokePaint));
    }

    private static void DrawSlash(IPdfCommandProcessor processor, float size, float lineWidth, SKColor color, PdfRenderingParameters renderingParameters)
    {
        var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            IsAntialias = renderingParameters.Antialias,
            Color = color
        };

        using var path = new SKPath();
        path.MoveTo(-size / 2, -size / 2);
        path.LineTo(size / 2, size / 2);
        processor.Process(new DrawPathCommand(path, paint));
    }
}
