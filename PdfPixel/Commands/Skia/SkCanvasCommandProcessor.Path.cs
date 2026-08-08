using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using SkiaSharp;

namespace PdfPixel.Commands.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteClipPath(ClipPathCommand command)
    {
        PdfStrokeScale strokeScale = GetStrokeScale(command.Paint);
        bool antialias = CommandHelpers.GetPathIsAntialias(command.Path, _executionContext, command.Paint, strokeScale);

        using SKPath sourcePath = command.Path.ToSkPath();
        using SKPaint? strokePaint = BuildClipStrokePaint(command, strokeScale);
        using SKPath clipPath = BuildClipPathGeometry(sourcePath, strokePaint, strokeScale);
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();

        _canvas.ClipPath(clipPath, skOperation, antialias);

        if (strokePaint != null && command.Paint != null)
        {
            _executionContext.Frames.OnClipStrokePath(command.Path, command.Operation, command.Paint);
        }
        else
        {
            _executionContext.Frames.OnClipPath(command.Path, command.Operation);
        }
    }

    private void ExecuteClipRectangle(ClipRectangleCommand command)
    {
        bool antialias = CommandHelpers.GetRectIsAntialias(command.Rect, _executionContext);
        PdfRectangle snappedRect = CommandHelpers.GetPixelSnappedRect(command.Rect, _executionContext);
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();
        _canvas.ClipRect(snappedRect.ToSkRect(), skOperation, antialias);
        _executionContext.Frames.OnClipRect(command.Rect, command.Operation);
    }

    private void ExecuteDrawPath(DrawPathCommand command)
    {
        // A degenerate fill covers no area, so it is painted as a hairline stroke to stay visible.
        bool isDegenerateFill = CommandHelpers.IsDegenerateFill(command.Path, command.Paint);
        PdfStrokeScale strokeScale = isDegenerateFill
            ? PdfStrokeScale.Create(_executionContext, 0f)
            : GetStrokeScale(command.Paint);

        using SKPath path = command.Path.ToSkPath();
        using SKPaint paint = command.Paint.ToSkiaPaint(strokeScale);
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);
        paint.IsAntialias = CommandHelpers.GetPathIsAntialias(command.Path, _executionContext, command.Paint, strokeScale);

        if (isDegenerateFill)
        {
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = (strokeScale.IsUniform) ? strokeScale.UniformWidth : strokeScale.PenWidth;
        }
        else if (command.Paint.Style != PdfPaintStyle.Stroke)
        {
            _canvas.DrawPath(path, paint);

            return;
        }

        SkStrokeGeometry.Draw(_canvas, path, paint, _executionContext, strokeScale);
    }

    private static SKPaint? BuildClipStrokePaint(ClipPathCommand command, in PdfStrokeScale strokeScale)
    {
        if (command.Paint == null || command.Paint.Style != PdfPaintStyle.Stroke)
        {
            return null;
        }

        return command.Paint.ToSkiaPaint(strokeScale);
    }

    private SKPath BuildClipPathGeometry(SKPath sourcePath, SKPaint? strokePaint, in PdfStrokeScale strokeScale)
    {
        if (strokePaint == null)
        {
            return new SKPath(sourcePath);
        }

        return SkStrokeGeometry.CreateOutline(sourcePath, strokePaint, _executionContext, strokeScale);
    }

    // A stroke paint widens by the scale its line width and the device matrix produce; anything else has
    // no pen to widen. A degenerate fill is the exception, and asks for the hairline scale directly.
    private PdfStrokeScale GetStrokeScale(PdfPaint? paint)
    {
        if (paint == null || paint.Style != PdfPaintStyle.Stroke)
        {
            return default;
        }

        return PdfStrokeScale.Create(_executionContext, paint.RequireStrokeStyle().LineWidth);
    }
}
