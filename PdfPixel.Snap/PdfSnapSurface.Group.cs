using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Rasterization;
using PdfPixel.Snap.Text;
using PdfPixel.Snap.Transparency;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    /// <summary>
    /// Composites what <paramref name="source"/> painted over this surface with Normal, alpha 1 and no soft mask, its
    /// pixels on this surface's pixels, within the current clip.
    /// </summary>
    /// <param name="source">The surface whose pixels are drawn; another surface than this one.</param>
    public void DrawSurface(PdfSnapSurface source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (ReferenceEquals(source, this))
        {
            throw new ArgumentException("A surface cannot be drawn onto itself.", nameof(source));
        }

        if (!_state.DeviceMatrix.IsIdentity)
        {
            // TODO: A surface placed by any matrix but the identity is still to be drawn, through the image path.
            throw new NotSupportedException("A surface can only be drawn under the identity matrix yet.");
        }

        PdfIntegerRectangle bounds = PdfIntegerRectangle.Intersect(source._root.PaintedBounds, GetDrawLimits());

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        PrepareTextObjectDraw(DefaultGroupPaint);
        new SnapRectangleGeometry(this, bounds).Draw(new SnapGroupRowPainter(this, source._root, DefaultGroupPaint));
    }

    /// <summary>
    /// Paints a group as one element over its own extent, within the clip; a surface without pixels only follows the
    /// content of the group's recording.
    /// </summary>
    private void DrawGroup(PdfGroupSource source, PdfPaint paint)
    {
        if (IsEmpty)
        {
            FollowGroupContent(source);
        }
        else
        {
            PaintGroup(source, paint);
        }
    }

    private void FollowGroupContent(PdfGroupSource source)
    {
        if (source.Tiling == null)
        {
            ExecuteDrawRecording(source.Recording);
        }
    }

    private void PaintGroup(PdfGroupSource source, PdfPaint paint)
    {
        PrepareTextObjectDraw(paint);

        PdfIntegerRectangle bounds = GetSourceBounds(source, GetDrawLimits());

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        DrawWithPaint(new SnapRectangleGeometry(this, bounds), bounds, paint, source);
    }

    /// <summary>
    /// Draws <paramref name="geometry"/> in the paint's color, or with <paramref name="source"/> as its source.
    /// </summary>
    private void DrawWithPaint<TGeometry>(TGeometry geometry, in PdfIntegerRectangle geometryBounds, PdfPaint paint, PdfGroupSource? source)
        where TGeometry : ISnapGeometry
    {
        if (source == null)
        {
            float alpha = ResolveColor(paint, _sourceColor);
            geometry.Draw(new SnapSolidRowPainter(this, _sourceColor, alpha, paint));
        }
        else if (source.Tiling != null && CanTileByRepeating(source, source.Tiling))
        {
            DrawTilingPeriods(geometry, source, source.Tiling, GetSourceBounds(source, geometryBounds), paint);
        }
        else
        {
            PdfIntegerRectangle groupBounds = GetSourceBounds(source, geometryBounds);
            SnapLayerGroup resolvedGroup = SnapLayerGroup.Resolve(source.Group, _state.Target);
            using SnapMemoryBlock block = Scratch.MemoryPool.Rent(SnapLayer.GetResultLength(groupBounds, resolvedGroup));
            using SnapLayer group = new(SnapLayer.CreateResult(groupBounds, resolvedGroup, block.Array), groupBounds, resolvedGroup, _state.Target, Scratch, GetConverters(), keepsShape: true);

            DrawGroupContent(source, group);
            geometry.Draw(new SnapGroupRowPainter(this, group, paint));
        }
    }

    private PdfIntegerRectangle GetDeviceBounds(in PdfRectangle userBounds)
        => PdfIntegerRectangle.Intersect(PdfSnapGrid.RoundOut(_state.DeviceMatrix.MapRect(userBounds)), GetDrawLimits());

    /// <summary>
    /// Returns the device pixels of <paramref name="userBounds"/> padded by one pixel for hairlines and shapes or
    /// images grown to a pixel, within the draw limits.
    /// </summary>
    private PdfIntegerRectangle GetContentDeviceBounds(in PdfRectangle userBounds)
    {
        PdfIntegerRectangle bounds = PdfSnapGrid.RoundOut(_state.DeviceMatrix.MapRect(userBounds));
        PdfIntegerRectangle paddedBounds = new(bounds.Left - 1, bounds.Top - 1, bounds.Right + 1, bounds.Bottom + 1);

        return PdfIntegerRectangle.Intersect(paddedBounds, GetDrawLimits());
    }

    private PdfIntegerRectangle GetSourceBounds(PdfGroupSource source, in PdfIntegerRectangle geometryBounds)
    {
        if (source.Bounds != null)
        {
            return PdfIntegerRectangle.Intersect(geometryBounds, GetContentDeviceBounds(source.Bounds.Value));
        }
        else
        {
            return geometryBounds;
        }
    }

    /// <summary>
    /// Draws the recording of <paramref name="source"/> into its group's <paramref name="layer"/> (ISO 32000-2 11.4),
    /// starting with no clip and no soft mask (11.6.6).
    /// </summary>
    private void DrawGroupContent(PdfGroupSource source, SnapLayer layer)
    {
        Save();
        _state.Target = layer;
        _state.Clip = null;
        _state.SetSoftMask(null);

        if (source.Tiling != null)
        {
            DrawTiledRecording(source, source.Tiling);
        }
        else
        {
            ExecuteDrawRecording(source.Recording);
        }

        Restore();
    }

    /// <summary>
    /// Starts drawing into <paramref name="layer"/> apart from the page: under <paramref name="deviceMatrix"/> and
    /// <paramref name="visibleRegion"/>, with no clip, soft mask, tint or open text object, adding nothing to the content
    /// tree, until <see cref="EndLocalDrawing"/>.
    /// </summary>
    private void BeginLocalDrawing(SnapLayer layer, in PdfMatrix deviceMatrix, PdfRectangle? visibleRegion)
    {
        SnapTextObject? textObject = _textObject;
        _textObject = null;

        Save();
        _state = SnapGraphicsState.CreateLocal(layer, deviceMatrix, visibleRegion, textObject);
    }

    /// <summary>
    /// Ends drawing started with <see cref="BeginLocalDrawing"/>: drops a text object it left open, restores to before
    /// its own save and takes back the text object set aside.
    /// </summary>
    private void EndLocalDrawing()
    {
        if (_state.LocalSaveDepth == null)
        {
            throw new InvalidOperationException("No local drawing is open.");
        }

        SnapTextObject? textObject = _state.SetAsideTextObject;

        if (_textObject != null)
        {
            DropTextObject(_textObject);
        }

        while (_state.LocalSaveDepth > 0)
        {
            Restore();
        }

        Restore();
        _textObject = textObject;
    }

    /// <summary>
    /// Begins a text object, closing one left open without the clip its glyphs gathered; a knockout group opens only
    /// with its first draw that needs one.
    /// </summary>
    private void BeginTextObject(bool knockout)
    {
        if (_textObject != null)
        {
            _textObject.ClipOutlines = null;
        }

        CloseTextObject();

        _textObject = new SnapTextObject(_state.Target, GetDrawLimits(), _savedStates.Count, knockout);
    }

    /// <summary>
    /// Opens the text object's knockout group (ISO 32000-2 9.3.8) for the first draw that is not opaque Normal, which
    /// would composite the same without it.
    /// </summary>
    private void PrepareTextObjectDraw(PdfPaint paint)
    {
        if (_textObject != null
            && _textObject.Knockout
            && _textObject.Group == null
            && ReferenceEquals(_state.Target, _textObject.Parent)
            && (paint.Alpha < 1 || !SnapBlendFunctions.IsNormal(paint.BlendMode)))
        {
            // TODO: Draws before the one opening the group are already composited into the parent, so they are not
            // knocked out by the draws after it.
            SnapLayerGroup resolvedGroup = SnapLayerGroup.Resolve(TextObjectGroup, _textObject.Parent);
            SnapMemoryBlock block = Scratch.MemoryPool.Rent(SnapLayer.GetResultLength(_textObject.Limits, resolvedGroup));
            _textObject.GroupBlock = block;
            PdfImageSamples result = SnapLayer.CreateResult(_textObject.Limits, resolvedGroup, block.Array);
            _textObject.Group = new SnapLayer(result, _textObject.Limits, resolvedGroup, _textObject.Parent, Scratch, GetConverters(), keepsShape: true);
            _state.Target = _textObject.Group;
        }
    }

    /// <summary>
    /// Ends the text object, compositing its knockout group with Normal, alpha 1 and no soft mask (ISO 32000-2 9.3.8),
    /// then intersecting the clip with the outlines its glyphs added, as one path by the non-zero winding rule (9.3.6).
    /// </summary>
#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void CloseTextObject()
    {
        if (_textObject == null)
        {
            return;
        }

        SnapTextObject textObject = _textObject;
        _textObject = null;

        if (textObject.Group != null)
        {
            SnapLayer group = textObject.Group;
            SnapLayer parent = textObject.Parent;
            PdfIntegerRectangle bounds = group.ElementBounds;
            SnapColorConverter? converter = parent.GetGroupConverter(group, DefaultGroupPaint);

            for (int y = bounds.Top; y < bounds.Bottom; y++)
            {
                parent.ComposeGroupRow(y, bounds.Left, bounds.Width, group, converter, SnapRowValues.One, SnapRowValues.One, DefaultGroupPaint);
            }

            if (ReferenceEquals(_state.Target, group))
            {
                _state.Target = parent;
            }
        }

        textObject.Dispose();

        if (textObject.ClipOutlines != null)
        {
            ApplyClip(textObject.ClipOutlines.ToPath(), PdfMatrix.Identity);
        }
    }

    /// <summary>
    /// Drops the text object a recording replayed after <paramref name="savesAfterOwnSave"/> saves left open when its
    /// replay failed: its knockout group is disposed without being composited.
    /// </summary>
    private void DiscardTextObject(int savesAfterOwnSave)
    {
        if (_textObject == null || _textObject.SaveCount < savesAfterOwnSave)
        {
            return;
        }

        DropTextObject(_textObject);
    }

    /// <summary>
    /// Drops <paramref name="textObject"/>, the open text object, without compositing it: its knockout group is
    /// disposed.
    /// </summary>
    private void DropTextObject(SnapTextObject textObject)
    {
        _textObject = null;

        if (textObject.Group != null && ReferenceEquals(_state.Target, textObject.Group))
        {
            _state.Target = textObject.Parent;
        }

        textObject.Dispose();
    }

    private readonly struct SnapRectangleGeometry : ISnapGeometry
    {
        private readonly PdfSnapSurface _surface;
        private readonly PdfIntegerRectangle _pixels;

        public SnapRectangleGeometry(PdfSnapSurface surface, in PdfIntegerRectangle pixels)
        {
            _surface = surface;
            _pixels = pixels;
        }

        /// <inheritdoc />
        public void Draw<TPainter>(TPainter painter)
            where TPainter : ISnapRowPainter
        {
            _surface.Draw(_pixels, PdfPathFillType.Winding, areaCoverage: false, painter);
        }
    }
}
