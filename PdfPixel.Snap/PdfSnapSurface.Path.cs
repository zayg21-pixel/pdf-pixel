using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Geometry.PathOperations;
using PdfPixel.Snap.Clipping;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Rasterization;
using System;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    // Relative difference below which a pen's two axis widths are treated as one.
    private const float PenUniformityTolerance = 0.01f;

    // A fill without area is stroked with the hairline pen: line width 0, one output pixel wide.
    private static readonly PdfStrokeStyle HairlineStrokeStyle = new PdfStrokeStyle().WithLineWidth(0f);

    /// <summary>
    /// Fills or strokes <paramref name="path"/>, or intersects the clip with the area it fills, as the style of
    /// <paramref name="paint"/> says.
    /// </summary>
    /// <param name="path">The path in current user space.</param>
    /// <param name="paint">The paint the path is drawn with.</param>
    public void DrawPath(PdfPath path, PdfPaint paint)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        PdfRectangle bounds;

        if (paint.Style == PdfPaintStyle.Stroke)
        {
            bounds = path.GetStrokeBounds(paint);
        }
        else
        {
            bounds = path.GetBounds();
        }

        DrawPath(path, paint, bounds, source: null);
    }

    /// <summary>
    /// Draws <paramref name="path"/>, or clips to it with a clip paint; a clip is never skipped for lying outside the
    /// draw limits, as it still clips everything away.
    /// </summary>
    private void DrawPath(PdfPath path, PdfPaint paint, PdfRectangle? bounds, PdfGroupSource? source)
    {
        if (IsEmpty)
        {
            return;
        }

        if (paint.Style == PdfPaintStyle.Clip)
        {
            ApplyClip(path, _state.DeviceMatrix);
        }
        else if (bounds == null || !IsOutsideDrawLimits(bounds.Value))
        {
            PrepareTextObjectDraw(paint);

            PdfPath outline = GetDrawingOutline(path, paint);

            DrawOutline(outline, snapsRectangle: !IsType3GlyphContent(), paint, source);
        }
    }

    /// <summary>
    /// Intersects the clip with the area <paramref name="outline"/> fills under <paramref name="deviceMatrix"/>; an
    /// axis-aligned rectangle clips to whole pixels without rasterizing, except in Type 3 glyph content.
    /// </summary>
    private void ApplyClip(PdfPath outline, in PdfMatrix deviceMatrix)
    {
        PdfIntegerRectangle targetBounds = _state.Target.Bounds;
        PdfIntegerRectangle? pixels = null;

        if (!IsType3GlyphContent())
        {
            pixels = PdfSnapGrid.GetShapePixels(outline, deviceMatrix);
        }

        if (pixels != null)
        {
            _state.Clip = SnapClip.IntersectRectangle(_state.Clip, pixels.Value, targetBounds);
        }
        else
        {
            Scratch.Edges.Clear();
            Scratch.Edges.AddPath(outline, deviceMatrix);

            _state.Clip = SnapClip.Intersect(_state.Clip, Scratch.Rasterizer, Scratch.Edges, outline.FillType, _executionContext.Parameters.Antialias, targetBounds, Scratch.ClipBuffer);
        }
    }

    /// <summary>
    /// Returns the outline to fill: the stroke outline for a stroke, a hairline for a fill without area, otherwise
    /// <paramref name="path"/>.
    /// </summary>
    private PdfPath GetDrawingOutline(PdfPath path, PdfPaint paint)
    {
        if (paint.Style == PdfPaintStyle.Stroke)
        {
            return BuildStrokeOutline(path, paint.RequireStrokeStyle());
        }

        PdfRectangle bounds = path.GetBounds();

        if (!path.IsEmpty && (bounds.Width == 0 || bounds.Height == 0))
        {
            return BuildStrokeOutline(path, HairlineStrokeStyle);
        }

        return path;
    }

    /// <summary>
    /// Builds the stroke outline of <paramref name="path"/> with a pen never thinner than one output pixel.
    /// </summary>
    private PdfPath BuildStrokeOutline(PdfPath path, PdfStrokeStyle strokeStyle)
    {
        (float penWidth, PdfMatrix penMatrix) = GetStrokePen(strokeStyle.LineWidth);

        return PdfStrokeOutlineBuilder.BuildOutline(path, strokeStyle.WithLineWidth(penWidth), penMatrix, _state.DeviceMatrix);
    }

    /// <summary>
    /// Returns the pen a stroke of <paramref name="lineWidth"/> is drawn with as its mark lands on the device pixels: the
    /// outline width, and the matrix shaping the pen, identity for a circular pen and a scaling for an elliptical one. A
    /// pen covering less than one device pixel along an axis is widened to cover exactly one, so a line width of zero or
    /// less, the PDF hairline, is one device pixel on both axes.
    /// </summary>
    private (float Width, PdfMatrix Matrix) GetStrokePen(float lineWidth)
    {
        PdfSize deviceScale = PdfSnapGrid.GetDeviceAxisLengths(_state.DeviceMatrix);

        if (deviceScale.Width <= 0 || deviceScale.Height <= 0)
        {
            return ((lineWidth > 0) ? lineWidth : 1f, PdfMatrix.Identity);
        }

        float widthX = (lineWidth * deviceScale.Width < 1f) ? 1f / deviceScale.Width : lineWidth;
        float widthY = (lineWidth * deviceScale.Height < 1f) ? 1f / deviceScale.Height : lineWidth;
        float wider = MathF.Max(widthX, widthY);

        if (MathF.Abs(widthX - widthY) <= PenUniformityTolerance * wider)
        {
            widthX = wider;
            widthY = wider;
        }

        // The shared width is a circular pen; the matrix carries the excess of the wider axis.
        float sharedWidth = MathF.Min(widthX, widthY);

        return (sharedWidth, PdfMatrix.CreateScale(widthX / sharedWidth, widthY / sharedWidth));
    }

    private void DrawOutline(PdfPath outline, bool snapsRectangle, PdfPaint paint, PdfGroupSource? source)
        => DrawWithPaint(new SnapOutlineGeometry(this, outline, snapsRectangle), GetDeviceBounds(outline.GetBounds()), paint, source);

    private readonly struct SnapOutlineGeometry : ISnapGeometry
    {
        private readonly PdfSnapSurface _surface;
        private readonly PdfPath _outline;
        private readonly bool _snapsRectangle;

        public SnapOutlineGeometry(PdfSnapSurface surface, PdfPath outline, bool snapsRectangle)
        {
            _surface = surface;
            _outline = outline;
            _snapsRectangle = snapsRectangle;
        }

        /// <inheritdoc />
        public void Draw<TPainter>(TPainter painter)
            where TPainter : ISnapRowPainter
        {
            _surface.DrawShape(_outline, _snapsRectangle, painter);
        }
    }
}
