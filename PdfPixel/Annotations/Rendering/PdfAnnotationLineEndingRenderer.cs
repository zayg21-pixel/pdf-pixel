using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
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
        float endingSize = GetEndingSize(lineWidth);

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ConcatMatrixCommand(GetEndingMatrix(x, y, otherX, otherY, style)));

        switch (style)
        {
            case PdfLineEndingStyle.OpenArrow:
                {
                    DrawOpenArrow(processor, endingSize, lineWidth, lineColor);
                    break;
                }
            case PdfLineEndingStyle.ClosedArrow:
                {
                    DrawClosedArrow(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case PdfLineEndingStyle.Square:
                {
                    DrawSquare(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case PdfLineEndingStyle.Circle:
                {
                    DrawCircle(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case PdfLineEndingStyle.Diamond:
                {
                    DrawDiamond(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case PdfLineEndingStyle.Butt:
                {
                    DrawButt(processor, endingSize, lineWidth, lineColor);
                    break;
                }
            case PdfLineEndingStyle.ROpenArrow:
                {
                    DrawROpenArrow(processor, endingSize, lineWidth, lineColor);
                    break;
                }
            case PdfLineEndingStyle.RClosedArrow:
                {
                    DrawRClosedArrow(processor, endingSize, lineWidth, lineColor, interiorColor);
                    break;
                }
            case PdfLineEndingStyle.Slash:
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
        PdfPath builtPath = CreateClosedArrowPath(size);

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
        PdfPath builtPath = CreateSquarePath(size);

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
        PdfPath builtPath = CreateCirclePath(size);

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
        PdfPath builtPath = CreateDiamondPath(size);

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
        path.MoveTo(-size, size / 2);
        path.LineTo(0, 0);
        path.LineTo(-size, -size / 2);

        processor.Process(new DrawPathCommand(path.ToPath(), paint));
    }

    private static void DrawRClosedArrow(IPdfCommandProcessor processor, float size, float lineWidth, in PdfColor color, PdfColor? interiorColor)
    {
        PdfPathBuilder path = new();
        path.MoveTo(0, 0);
        path.LineTo(-size, size / 2);
        path.LineTo(-size, -size / 2);
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

    private static float GetEndingSize(float lineWidth) => lineWidth * 6f;

    private static PdfMatrix GetEndingMatrix(float x, float y, float otherX, float otherY, PdfLineEndingStyle style)
    {
        if (style == PdfLineEndingStyle.Square || style == PdfLineEndingStyle.Circle)
        {
            return PdfMatrix.CreateTranslation(x, y);
        }

        float angle = MathF.Atan2(otherY - y, otherX - x);

        return PdfMatrix.Concat(PdfMatrix.CreateTranslation(x, y), PdfMatrix.CreateRotationDegrees(angle * 180 / MathF.PI));
    }

    private static PdfPath CreateClosedArrowPath(float size)
    {
        PdfPathBuilder path = new();
        path.MoveTo(0, 0);
        path.LineTo(size, -size / 2);
        path.LineTo(size, size / 2);
        path.Close();

        return path.ToPath();
    }

    private static PdfPath CreateSquarePath(float size)
    {
        PdfPathBuilder path = new();
        path.AddRect(PdfRectangle.FromLocationAndSize(-size / 2, -size / 2, size, size));

        return path.ToPath();
    }

    private static PdfPath CreateCirclePath(float size)
    {
        PdfPathBuilder path = new();
        path.AddCircle(PdfPoint.Empty, size / 2);

        return path.ToPath();
    }

    private static PdfPath CreateDiamondPath(float size)
    {
        PdfPathBuilder path = new();
        path.MoveTo(size / 2, 0);
        path.LineTo(0, -size / 2);
        path.LineTo(-size / 2, 0);
        path.LineTo(0, size / 2);
        path.Close();

        return path.ToPath();
    }
}
