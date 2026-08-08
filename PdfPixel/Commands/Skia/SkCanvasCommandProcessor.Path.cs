using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using SkiaSharp;

namespace PdfPixel.Commands.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    // The pen a degenerate fill is painted with. Its width comes from the hairline stroke scale, so the
    // width here carries no meaning; the caps and joins are the ones Skia stroked such a fill with.
    private static readonly PdfStrokeStyle HairlineStrokeStyle = new();

    private void ExecuteClipPath(ClipPathCommand command)
    {
        PdfStrokeScale strokeScale = GetStrokeScale(command.Paint);
        bool antialias = CommandHelpers.GetPathIsAntialias(command.Path.GetPathInfo(), _executionContext, command.Paint, strokeScale);

        PdfPaint? strokePaint = GetStrokePaint(command.Paint);
        PdfPath clipGeometry = (strokePaint != null)
            ? PdfStrokeOutline.Build(command.Path, strokePaint.RequireStrokeStyle(), strokeScale)
            : command.Path;

        using SKPath clipPath = clipGeometry.ToSkPath();
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();

        _canvas.ClipPath(clipPath, skOperation, antialias);

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
        bool antialias = CommandHelpers.GetRectIsAntialias(command.Rect, _executionContext);
        PdfRectangle snappedRect = CommandHelpers.GetPixelSnappedRect(command.Rect, _executionContext);
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();
        _canvas.ClipRect(snappedRect.ToSkRect(), skOperation, antialias);
        _executionContext.Frames.OnClipRect(command.Rect, command.Operation);
    }

    private void ExecuteDrawPath(DrawPathCommand command)
    {
        PdfPathInfo pathInfo = command.Path.GetPathInfo();

        // A degenerate fill covers no area, so it is painted as a hairline stroke to stay visible.
        bool isDegenerateFill = CommandHelpers.IsDegenerateFill(pathInfo, command.Paint);

        if (!isDegenerateFill && command.Paint.Style != PdfPaintStyle.Stroke)
        {
            // Snapped geometry sits on pixel boundaries, where coverage has nothing left to blend. Only a
            // rectangle gets here, so its bounds describe it completely.
            bool snapToPixels = CommandHelpers.CanSnapPath(pathInfo, _executionContext);
            PdfPath fillGeometry = snapToPixels
                ? BuildSnappedRectanglePath(pathInfo, command.Path.FillType)
                : command.Path;

            using SKPath fillPath = fillGeometry.ToSkPath();
            using SKPaint fillPaint = command.Paint.ToSkiaPaint();
            SkiaCommandUtilities.ModifyPaint(fillPaint, _executionContext);
            fillPaint.IsAntialias = !snapToPixels
                && CommandHelpers.GetPathIsAntialias(pathInfo, _executionContext, command.Paint, default);

            _canvas.DrawPath(fillPath, fillPaint);

            return;
        }

        PdfStrokeScale strokeScale = isDegenerateFill
            ? PdfStrokeScale.Create(_executionContext, 0f)
            : GetStrokeScale(command.Paint);

        PdfStrokeStyle strokeStyle = isDegenerateFill
            ? HairlineStrokeStyle
            : command.Paint.RequireStrokeStyle();

        PdfPath outline = PdfStrokeOutline.Build(command.Path, strokeStyle, strokeScale);

        using SKPath outlinePath = outline.ToSkPath();
        using SKPaint outlinePaint = command.Paint.ToSkiaOutlineFillPaint();
        SkiaCommandUtilities.ModifyPaint(outlinePaint, _executionContext);
        outlinePaint.IsAntialias = CommandHelpers.GetPathIsAntialias(pathInfo, _executionContext, command.Paint, strokeScale);

        _canvas.DrawPath(outlinePath, outlinePaint);
    }

    private static PdfPaint? GetStrokePaint(PdfPaint? paint)
        => (paint != null && paint.Style == PdfPaintStyle.Stroke) ? paint : null;

    private PdfPath BuildSnappedRectanglePath(in PdfPathInfo pathInfo, PdfPathFillType fillType)
    {
        PdfRectangle snappedRect = CommandHelpers.SnapRectToDevicePixels(pathInfo.Bounds, CommandHelpers.GetScaledMatrix(_executionContext));

        PdfPathBuilder builder = new();
        builder.AddRect(snappedRect);

        return builder.ToPath(fillType);
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
