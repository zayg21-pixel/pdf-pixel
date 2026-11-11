using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Geometry;
using PdfPixel.Rendering.Text;
using PdfPixel.Snap.Clipping;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Rasterization;
using PdfPixel.Snap.Text;
using PdfPixel.Text;
using System;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    /// <summary>
    /// Fills or strokes shaped <paramref name="glyphs"/>, or adds their outlines to the clip, as the style of
    /// <paramref name="paint"/> says. Inside a text object the outlines clip when it ends, otherwise at once.
    /// </summary>
    /// <param name="glyphs">The shaped glyphs, in text space.</param>
    /// <param name="matrix">The matrix from text space to current user space.</param>
    /// <param name="paint">The paint the glyphs are drawn with.</param>
    public void DrawText(in ReadOnlyMemory<ShapedGlyph> glyphs, in PdfMatrix matrix, PdfPaint paint)
    {
        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        PdfRectangle? bounds = PdfTextRenderUtilities.GetTextBounds(glyphs.Span, matrix);

        if (bounds != null && paint.Style == PdfPaintStyle.Stroke)
        {
            bounds = bounds.Value.InflateForStroke(paint);
        }

        DrawText(glyphs, matrix, paint, bounds, source: null);
    }

    /// <summary>
    /// Draws text antialiased, never snapped to whole pixels: filled text from glyph stencils, stroked text from its
    /// stroke outline.
    /// </summary>
    private void DrawText(in ReadOnlyMemory<ShapedGlyph> glyphs, in PdfMatrix matrix, PdfPaint paint, PdfRectangle? textBounds, PdfGroupSource? source)
    {
        if (glyphs.IsEmpty || IsEmpty)
        {
            return;
        }

        lock (_executionContext.ContentLocker)
        {
            if (paint.Style == PdfPaintStyle.Clip)
            {
                AddTextClip(glyphs, matrix);
            }
            else if (textBounds == null || !IsOutsideDrawLimits(textBounds.Value))
            {
                PrepareTextObjectDraw(paint);

                if (paint.Style == PdfPaintStyle.Stroke)
                {
                    PdfPath textPath = PdfTextRenderUtilities.GetTextPath(glyphs.Span, matrix);

                    DrawOutline(BuildStrokeOutline(textPath, paint.RequireStrokeStyle()), snapsRectangle: false, paint, source);
                }
                else
                {
                    PdfIntegerRectangle glyphBounds = (textBounds != null) ? GetDeviceBounds(textBounds.Value) : GetDrawLimits();

                    DrawWithPaint(new SnapGlyphGeometry(this, glyphs, matrix), glyphBounds, paint, source);
                }
            }
        }
    }

    /// <summary>
    /// Adds the glyphs' outlines to the open text object's clip, or clips to them at once outside a text object;
    /// glyphs without outlines add nothing, so a text object showing only those does not clip (ISO 32000-2 9.3.6).
    /// </summary>
    private void AddTextClip(in ReadOnlyMemory<ShapedGlyph> glyphs, in PdfMatrix matrix)
    {
        PdfPath textPath = PdfTextRenderUtilities.GetTextPath(glyphs.Span, matrix);

        if (textPath.IsEmpty)
        {
            return;
        }

        if (_textObject != null)
        {
            if (_textObject.ClipOutlines == null)
            {
                _textObject.ClipOutlines = new PdfPathBuilder();
            }

            _textObject.ClipOutlines.AddPath(textPath.Transform(_state.DeviceMatrix));
        }
        else
        {
            ApplyClip(textPath, _state.DeviceMatrix);
        }
    }

    private void DrawGlyphStencils<TPainter>(in ReadOnlyMemory<ShapedGlyph> glyphs, in PdfMatrix matrix, TPainter painter)
        where TPainter : ISnapRowPainter
    {
        SnapShapeRowSink<TPainter> rowSink = new(painter, Scratch);

        if (_state.Clip == null || _state.Clip.IsRectangle)
        {
            DrawGlyphStencils(glyphs, matrix, ref rowSink);
        }
        else
        {
            SnapClippedCoverageSink<SnapShapeRowSink<TPainter>> clippedSink = new(_state.Clip, Scratch.ClipBuffer, rowSink);
            DrawGlyphStencils(glyphs, matrix, ref clippedSink);
        }
    }

    /// <summary>
    /// Places each glyph's stencil at its origin rounded to the nearest 1/<see cref="SnapGlyphStencilCache.SubpixelSteps"/>
    /// pixel.
    /// </summary>
    private void DrawGlyphStencils<TSink>(in ReadOnlyMemory<ShapedGlyph> glyphs, in PdfMatrix matrix, ref TSink sink)
        where TSink : ISnapCoverageSink
    {
        PdfMatrix textToDevice = _state.DeviceMatrix.PreConcat(matrix);
        PdfIntegerRectangle limits = GetDrawLimits();
        bool areaCoverage = _executionContext.Parameters.Antialias;
        SnapGlyphStencilCache? cache = null;

        foreach (ShapedGlyph glyph in glyphs.Span)
        {
            ushort? glyphId = glyph.GlyphId;

            if (glyphId == null)
            {
                continue;
            }

            IPdfTypeface typeface = glyph.CharacterInfo.Typeface;

            if (cache == null || !ReferenceEquals(cache.Typeface, typeface))
            {
                cache = SnapGlyphStencilCache.Get(_executionContext.Cache, typeface);
            }

            PdfMatrix glyphToDevice = textToDevice.PreConcat(PdfMatrix.CreateScale(glyph.Scale, -1f));
            PdfPoint origin = textToDevice.MapPoint(new PdfPoint(glyph.X, glyph.Y));
            var stepX = (int)Math.Floor((origin.X * SnapGlyphStencilCache.SubpixelSteps) + 0.5f);
            var stepY = (int)Math.Floor((origin.Y * SnapGlyphStencilCache.SubpixelSteps) + 0.5f);
            int originX = FloorDivide(stepX, SnapGlyphStencilCache.SubpixelSteps);
            int originY = FloorDivide(stepY, SnapGlyphStencilCache.SubpixelSteps);
            int subpixelX = stepX - (originX * SnapGlyphStencilCache.SubpixelSteps);
            int subpixelY = stepY - (originY * SnapGlyphStencilCache.SubpixelSteps);

            SnapStencil? stencil = cache.GetStencil(glyphId.Value, glyphToDevice, subpixelX, subpixelY, areaCoverage, Scratch.Edges, Scratch.Rasterizer);

            if (stencil != null)
            {
                PlaceStencil(stencil, originX + stencil.Left, originY + stencil.Top, limits, ref sink);
            }
        }
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);

    private readonly struct SnapGlyphGeometry : ISnapGeometry
    {
        private readonly PdfSnapSurface _surface;
        private readonly ReadOnlyMemory<ShapedGlyph> _glyphs;
        private readonly PdfMatrix _matrix;

        public SnapGlyphGeometry(PdfSnapSurface surface, in ReadOnlyMemory<ShapedGlyph> glyphs, in PdfMatrix matrix)
        {
            _surface = surface;
            _glyphs = glyphs;
            _matrix = matrix;
        }

        /// <inheritdoc />
        public void Draw<TPainter>(TPainter painter)
            where TPainter : ISnapRowPainter
        {
            _surface.DrawGlyphStencils(_glyphs, _matrix, painter);
        }
    }
}
