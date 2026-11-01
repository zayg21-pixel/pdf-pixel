using PdfPixel.Annotations;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// Utility class for drawing line endings on annotations.
/// </summary>
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
    /// <param name="linePaint">The paint whose color the ending is stroked in, or null when the line has none.</param>
    /// <param name="interiorPaint">The paint closed shapes are filled with, or null when they have no interior.</param>
    public static void DrawLineEnding(
        IPdfCommandProcessor processor,
        float x,
        float y,
        float otherX,
        float otherY,
        PdfLineEndingStyle style,
        float lineWidth,
        PdfPaint? linePaint,
        PdfPaint? interiorPaint)
    {
        float endingSize = GetEndingSize(lineWidth);

        processor.Process(SaveStateCommand.Instance);
        processor.Process(new ConcatMatrixCommand(GetEndingMatrix(x, y, otherX, otherY, style)));

        switch (style)
        {
            case PdfLineEndingStyle.OpenArrow:
                {
                    DrawOpenArrow(processor, endingSize, lineWidth, linePaint);
                    break;
                }
            case PdfLineEndingStyle.ClosedArrow:
                {
                    DrawClosedArrow(processor, endingSize, lineWidth, linePaint, interiorPaint);
                    break;
                }
            case PdfLineEndingStyle.Square:
                {
                    DrawSquare(processor, endingSize, lineWidth, linePaint, interiorPaint);
                    break;
                }
            case PdfLineEndingStyle.Circle:
                {
                    DrawCircle(processor, endingSize, lineWidth, linePaint, interiorPaint);
                    break;
                }
            case PdfLineEndingStyle.Diamond:
                {
                    DrawDiamond(processor, endingSize, lineWidth, linePaint, interiorPaint);
                    break;
                }
            case PdfLineEndingStyle.Butt:
                {
                    DrawButt(processor, endingSize, lineWidth, linePaint);
                    break;
                }
            case PdfLineEndingStyle.ROpenArrow:
                {
                    DrawROpenArrow(processor, endingSize, lineWidth, linePaint);
                    break;
                }
            case PdfLineEndingStyle.RClosedArrow:
                {
                    DrawRClosedArrow(processor, endingSize, lineWidth, linePaint, interiorPaint);
                    break;
                }
            case PdfLineEndingStyle.Slash:
                {
                    DrawSlash(processor, endingSize, lineWidth, linePaint);
                    break;
                }
        }

        processor.Process(RestoreStateCommand.Instance);
    }

    /// <summary>
    /// Returns an upper bound of how far the line endings <paramref name="start"/> and <paramref name="end"/> drawn
    /// with <paramref name="lineWidth"/> reach from the ends of their line, 0 when both are none.
    /// </summary>
    public static float GetEndingsReach(PdfLineEndingStyle start, PdfLineEndingStyle end, float lineWidth)
    {
        if (start == PdfLineEndingStyle.None && end == PdfLineEndingStyle.None)
        {
            return 0f;
        }

        return 2f * GetEndingSize(lineWidth);
    }

    private static void DrawOpenArrow(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint)
    {
        PdfPathBuilder path = new();
        path.MoveTo(size, -size / 2);
        path.LineTo(0, 0);
        path.LineTo(size, size / 2);

        DrawStroke(processor, path.ToPath(), lineWidth, paint);
    }

    private static void DrawClosedArrow(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint, PdfPaint? interiorPaint)
        => DrawClosedShape(processor, CreateClosedArrowPath(size), lineWidth, paint, interiorPaint);

    private static void DrawSquare(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint, PdfPaint? interiorPaint)
        => DrawClosedShape(processor, CreateSquarePath(size), lineWidth, paint, interiorPaint);

    private static void DrawCircle(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint, PdfPaint? interiorPaint)
        => DrawClosedShape(processor, CreateCirclePath(size), lineWidth, paint, interiorPaint);

    private static void DrawDiamond(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint, PdfPaint? interiorPaint)
        => DrawClosedShape(processor, CreateDiamondPath(size), lineWidth, paint, interiorPaint);

    private static void DrawButt(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint)
    {
        PdfPathBuilder path = new();
        path.MoveTo(0, -size / 2);
        path.LineTo(0, size / 2);

        DrawStroke(processor, path.ToPath(), lineWidth, paint);
    }

    private static void DrawROpenArrow(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint)
    {
        PdfPathBuilder path = new();
        path.MoveTo(-size, size / 2);
        path.LineTo(0, 0);
        path.LineTo(-size, -size / 2);

        DrawStroke(processor, path.ToPath(), lineWidth, paint);
    }

    private static void DrawRClosedArrow(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint, PdfPaint? interiorPaint)
    {
        PdfPathBuilder path = new();
        path.MoveTo(0, 0);
        path.LineTo(-size, size / 2);
        path.LineTo(-size, -size / 2);
        path.Close();

        DrawClosedShape(processor, path.ToPath(), lineWidth, paint, interiorPaint);
    }

    private static void DrawSlash(IPdfCommandProcessor processor, float size, float lineWidth, PdfPaint? paint)
    {
        PdfPathBuilder path = new();
        path.MoveTo(-size / 2, -size / 2);
        path.LineTo(size / 2, size / 2);

        DrawStroke(processor, path.ToPath(), lineWidth, paint);
    }

    private static void DrawClosedShape(IPdfCommandProcessor processor, PdfPath path, float lineWidth, PdfPaint? paint, PdfPaint? interiorPaint)
    {
        if (interiorPaint != null)
        {
            processor.Process(new DrawPathCommand(path, interiorPaint));
        }

        DrawStroke(processor, path, lineWidth, paint);
    }

    private static void DrawStroke(IPdfCommandProcessor processor, PdfPath path, float lineWidth, PdfPaint? paint)
    {
        if (paint == null)
        {
            return;
        }

        processor.Process(new DrawPathCommand(path, PdfAnnotationPaintFactory.CreateStrokePaint(paint, lineWidth)));
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
