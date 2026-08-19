using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using SkiaSharp;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteClipPath(ClipPathCommand command)
    {
        // A clip edge blends with the coverage of everything drawn inside it, so putting the edge on a
        // pixel boundary is what keeps that edge from tinting the content along it.
        PdfPathDeviceGeometry clipGeometry = PdfPathDeviceGeometry.CreateForClipping(command.Path, command.Paint, _executionContext);

        using SKPath clipPath = clipGeometry.GetPath().ToSkPath();
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();

        _canvas.ClipPath(clipPath, skOperation, clipGeometry.IsAntialias);

        PdfPaint? strokePaint = GetStrokePaint(command.Paint);

        if (strokePaint != null)
        {
            _executionContext.Frames.OnClipStrokePath(command.Path, command.Operation, strokePaint);
        }
        else
        {
            _executionContext.Frames.OnClipPath(command.Path, command.Operation);
        }
    }

    private void ExecuteClipRectangle(ClipRectangleCommand command)
    {
        PdfRectangle? sourceRect = (command.Source == PdfClipRectangleSource.Region)
            ? GetRegionOfInterestRect()
            : command.Rect;

        if (sourceRect == null)
        {
            return;
        }

        PdfRectangle rect = sourceRect.Value;
        PdfPathDeviceGeometry clipGeometry = PdfPathDeviceGeometry.CreateForClipping(rect, _executionContext);
        PdfRectangle clipRect = clipGeometry.SnappedRectangle ?? rect;
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();

        _canvas.ClipRect(clipRect.ToSkRect(), skOperation, clipGeometry.IsAntialias);
        _executionContext.Frames.OnClipRect(rect, command.Operation);
    }

    /// <summary>
    /// Maps the visible page region into current user space, so it passes through the same device
    /// mapping as any other clip rectangle. Returns null when the whole page is visible.
    /// </summary>
    private PdfRectangle? GetRegionOfInterestRect()
    {
        if (!_executionContext.PageRegionOfInterest.HasValue)
        {
            return null;
        }

        return _executionContext.Frames.TotalMatrix.Invert().MapRect(_executionContext.PageRegionOfInterest.Value);
    }

    private void ExecuteDrawPath(DrawPathCommand command)
    {
        PdfPathDeviceGeometry geometry = PdfPathDeviceGeometry.CreateForDrawing(command.Path, command.Paint, _executionContext);

        using SKPath path = geometry.GetPath().ToSkPath();
        using SKPaint paint = (geometry.IsStrokeOutline) ? command.Paint.ToSkiaOutlineFillPaint() : command.Paint.ToSkiaPaint();
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);
        paint.IsAntialias = geometry.IsAntialias;

        _canvas.DrawPath(path, paint);
    }

    private static PdfPaint? GetStrokePaint(PdfPaint? paint)
        => (paint != null && paint.Style == PdfPaintStyle.Stroke) ? paint : null;
}
