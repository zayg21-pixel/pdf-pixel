using PdfPixel.Annotations.Models;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
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
    public static void DrawLineEnding(
        IPdfCommandProcessor processor,
        float x,
        float y,
        float otherX,
        float otherY,
        PdfLineEndingStyle style,
        float lineWidth,
        in PdfColor lineColor,
        PdfColor? interiorColor)
    {
        float dx = otherX - x;
        float dy = otherY - y;
        var angle = (float)Math.Atan2(dy, dx);
        float endingSize = lineWidth * 3;

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateTranslation(x, y)));
        processor.Process(new ConcatMatrixCommand(PdfMatrix.CreateRotationDegrees(angle * 180 / MathF.PI)));

        switch (style)
        {
            case Models.PdfLineEndingStyle.OpenArrow:
                {
                    DrawOpenArrow(processor, endingSize, lineWidth, lineColor);
                    break;
                }
            case Models.PdfLineEndingStyle.ClosedArrow:
                {
                    DrawClosedArrow(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case Models.PdfLineEndingStyle.Square:
                {
                    DrawSquare(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case Models.PdfLineEndingStyle.Circle:
                {
                    DrawCircle(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case Models.PdfLineEndingStyle.Diamond:
                {
                    DrawDiamond(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case Models.PdfLineEndingStyle.Butt:
                {
                    DrawButt(processor, endingSize, lineWidth, lineColor);
                    break;
                }
            case Models.PdfLineEndingStyle.ROpenArrow:
                {
                    DrawROpenArrow(processor, endingSize, lineWidth, lineColor);
                    break;
                }
            case Models.PdfLineEndingStyle.RClosedArrow:
                {
                    DrawRClosedArrow(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case Models.PdfLineEndingStyle.Slash:
                {
                    DrawSlash(processor, endingSize, lineWidth, lineColor);
                    break;
                }
        }

        processor.Process(RestoreStateCommand.Instance);
    }

    private static void DrawOpenArrow(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color)
    {
        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);

        PdfPathBuilder path = new();
        path.MoveTo(size, -size / 2);
        path.LineTo(0, 0);
        path.LineTo(size, size / 2);

        processor.Process(new DrawPathCommand(path.ToPath(), paint));
    }

    private static void DrawClosedArrow(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color, PdfColor? interiorColor)
    {
        PdfPathBuilder path = new();
        path.MoveTo(0, 0);
        path.LineTo(size, -size / 2);
        path.LineTo(size, size / 2);
        path.Close();

        PdfPath builtPath = path.ToPath();

        if (interiorColor.HasValue && interiorColor.Value != PdfColors.Transparent)
        {
            PdfPaint fillPaint = PdfAnnotationPaintFactory.CreateFillPaint(interiorColor.Value);
            processor.Process(new DrawPathCommand(builtPath, fillPaint));
        }

        PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);
        processor.Process(new DrawPathCommand(builtPath, strokePaint));
    }

    private static void DrawSquare(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color, PdfColor? interiorColor)
    {
        PdfRectangle rect = new(-size / 2, -size / 2, size / 2, size / 2);

        PdfPathBuilder path = new();
        path.AddRect(rect);

        PdfPath builtPath = path.ToPath();

        if (interiorColor.HasValue && interiorColor.Value != PdfColors.Transparent)
        {
            PdfPaint fillPaint = PdfAnnotationPaintFactory.CreateFillPaint(interiorColor.Value);
            processor.Process(new DrawPathCommand(builtPath, fillPaint));
        }

        PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);
        processor.Process(new DrawPathCommand(builtPath, strokePaint));
    }

    private static void DrawCircle(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color, PdfColor? interiorColor)
    {
        float radius = size / 2;

        PdfPathBuilder path = new();
        path.AddCircle(PdfPoint.Empty, radius);

        PdfPath builtPath = path.ToPath();

        if (interiorColor.HasValue && interiorColor.Value != PdfColors.Transparent)
        {
            PdfPaint fillPaint = PdfAnnotationPaintFactory.CreateFillPaint(interiorColor.Value);
            processor.Process(new DrawPathCommand(builtPath, fillPaint));
        }

        PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);
        processor.Process(new DrawPathCommand(builtPath, strokePaint));
    }

    private static void DrawDiamond(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color, PdfColor? interiorColor)
    {
        PdfPathBuilder path = new();
        path.MoveTo(size / 2, 0);
        path.LineTo(0, -size / 2);
        path.LineTo(-size / 2, 0);
        path.LineTo(0, size / 2);
        path.Close();

        PdfPath builtPath = path.ToPath();

        if (interiorColor.HasValue && interiorColor.Value != PdfColors.Transparent)
        {
            PdfPaint fillPaint = PdfAnnotationPaintFactory.CreateFillPaint(interiorColor.Value);
            processor.Process(new DrawPathCommand(builtPath, fillPaint));
        }

        PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);
        processor.Process(new DrawPathCommand(builtPath, strokePaint));
    }

    private static void DrawButt(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color)
    {
        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);

        PdfPathBuilder path = new();
        path.MoveTo(0, -size / 2);
        path.LineTo(0, size / 2);
        processor.Process(new DrawPathCommand(path.ToPath(), paint));
    }

    private static void DrawROpenArrow(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color)
    {
        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);

        PdfPathBuilder path = new();
        path.MoveTo(size, size / 2);
        path.LineTo(0, 0);
        path.LineTo(size, -size / 2);

        processor.Process(new DrawPathCommand(path.ToPath(), paint));
    }

    private static void DrawRClosedArrow(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color, PdfColor? interiorColor)
    {
        PdfPathBuilder path = new();
        path.MoveTo(0, 0);
        path.LineTo(size, size / 2);
        path.LineTo(size, -size / 2);
        path.Close();

        PdfPath builtPath = path.ToPath();

        if (interiorColor.HasValue && interiorColor.Value != PdfColors.Transparent)
        {
            PdfPaint fillPaint = PdfAnnotationPaintFactory.CreateFillPaint(interiorColor.Value);
            processor.Process(new DrawPathCommand(builtPath, fillPaint));
        }

        PdfPaint strokePaint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);
        processor.Process(new DrawPathCommand(builtPath, strokePaint));
    }

    private static void DrawSlash(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color)
    {
        PdfPaint paint = PdfAnnotationPaintFactory.CreateStrokePaint(color, lineWidth);

        PdfPathBuilder path = new();
        path.MoveTo(-size / 2, -size / 2);
        path.LineTo(size / 2, size / 2);
        processor.Process(new DrawPathCommand(path.ToPath(), paint));
    }
}
